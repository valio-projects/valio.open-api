const http = require('node:http');
const axios = require('axios');
const events = [];
let token = 'expired';
let refreshCount = 0;
let requests = 0;
let alwaysUnauthorized = false;
let hang = false;
let requestStarted;
const server = http.createServer((request, response) => {
  requests++;
  request.resume();
  if (hang) {
    requestStarted();
    return;
  }
  response.setHeader('Content-Type', 'application/json');
  if (alwaysUnauthorized || request.headers.authorization !== 'Bearer refreshed') {
    response.writeHead(401);
    response.end(JSON.stringify({ expired: true }));
    return;
  }
  response.end(JSON.stringify('accepted'));
});

(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  const refresh = async () => {
    events.push('refresh');
    refreshCount++;
    token = 'refreshed';
  };
  let client;
  if (transport === 'fetch') {
    client = async (input, options) => {
      for (let attempt = 0; attempt < 2; attempt++) {
        options.signal?.throwIfAborted();
        const headers = new Headers(options.headers);
        headers.set('Authorization', `Bearer ${token}`);
        events.push(`before:${token}`);
        const response = await fetch(new URL(input, base), { ...options, headers });
        events.push(`after:${response.status}`);
        if (response.status !== 401 || attempt === 1) return response;
        await response.body?.cancel();
        await refresh();
      }
      throw new Error('Unreachable retry state');
    };
  } else {
    client = axios.create({ baseURL: base });
    client.interceptors.request.use(config => {
      config.headers.set('Authorization', `Bearer ${token}`);
      events.push(`before:${token}`);
      return config;
    });
    client.interceptors.response.use(async response => {
      events.push(`after:${response.status}`);
      if (response.status === 401 && !response.config.valioRetried) {
        response.config.valioRetried = true;
        await refresh();
        return client.request(response.config);
      }
      return response;
    });
  }
  let configuredCalls = 0;
  if (transport === 'axios') {
    const configured = axios.create({ baseURL: base });
    configured.interceptors.request.use(config => {
      configuredCalls++;
      config.headers.set('Authorization', 'Bearer refreshed');
      return config;
    });
    configured.interceptors.response.use(response => ({ ...response, data: response.data + '-configured' }));
    const Module = require('node:module');
    const originalLoad = Module._load;
    Module._load = function(name, ...args) {
      if (name === '@app/native-client') return { apiMutator: configured };
      return originalLoad.call(this, name, ...args);
    };
  }
  const { mixed } = require(path.join(root, 'api', 'mixed', `mixed.${transport}.js`));
  try {
    const result = await mixed({ body: 'input' }, undefined, undefined, client);
    assert.deepEqual(result, { status: 200, body: 'accepted', isSuccessful: true, isFailed: false });
    assert.equal(requests, 2);
    assert.equal(refreshCount, 1);
    assert.deepEqual(events, ['before:expired', 'after:401', 'refresh', 'before:refreshed', 'after:200']);
    assert.equal(configuredCalls, 0, 'The explicit native client must override the configured default');
    if (transport === 'axios') {
      const configuredResult = await mixed({ body: 'input' });
      assert.equal(configuredResult.body, 'accepted-configured', 'The configured native response interceptor must run');
      assert.equal(configuredCalls, 1);
      assert.equal(requests, 3);
    }

    events.length = 0;
    requests = 0;
    refreshCount = 0;
    alwaysUnauthorized = true;
    const unauthorized = await mixed({ body: 'input' }, undefined, undefined, client);
    assert.equal(unauthorized.status, 401);
    assert.equal(unauthorized.isFailed, true);
    assert.equal(requests, 2, 'A failed refresh must never trigger an unbounded replay loop');
    assert.equal(refreshCount, 1);

    hang = true;
    const controller = new AbortController();
    const reason = new Error('cancel requested by caller');
    let deadline;
    const started = new Promise((resolve, reject) => {
      requestStarted = resolve;
      deadline = setTimeout(() => reject(new Error('The native request was not dispatched')), 5000);
    });
    const pending = mixed({ body: 'input' }, undefined, controller.signal, client);
    try {
      await started;
      controller.abort(reason);
      await assert.rejects(pending, error => transport === 'fetch' ? error === reason : axios.isCancel(error));
    } finally {
      clearTimeout(deadline);
    }
    assert.equal(refreshCount, 1, 'Cancellation must not refresh or replay a request');

    const preAborted = new AbortController();
    preAborted.abort(reason);
    const before = requests;
    await assert.rejects(mixed({ body: 'input' }, undefined, preAborted.signal, client),
      error => transport === 'fetch' ? error === reason : axios.isCancel(error));
    assert.equal(requests, before, 'Pre-aborted requests must not reach the HTTP server');

    await assert.rejects(mixed({ body: 42 }, undefined, undefined, client));
    assert.equal(requests, before, 'Request validation must run before the injected transport');
    console.log('native HTTP injection runtime passed');
  } finally {
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
