---
title: FAQ
description: Häufig gestellte Fragen.
translationKey: faq
---

## Warum eine einzelne CLI-Binary statt eines NuGet-Generators?

NuGet-Quellgeneratoren laufen zur Kompilierzeit im Projekt des
Konsumenten. ValIO ist OpenAPI-zentriert — die meisten Nutzerinnen und
Nutzer möchten dasselbe Generator-Ergebnis über mehrere Repositories und
Sprachen hinweg reproduzieren, nicht nur innerhalb eines einzelnen
C#-Projekts. Ein eigenständiges Werkzeug passt dazu besser und hält den
Generator unabhängig von der Laufzeit des Konsumenten.

## Kann ich ValIO offline verwenden?

Ja. CLI und Desktop-App laufen vollständig offline, sobald Sie eine
lokale Kopie der OpenAPI-Schemadatei haben. Die Web-UI lädt Schemas nur
dann über das Netzwerk, wenn Sie eine URL einfügen — hochgeladene
Dateien werden lokal verarbeitet.

## Wie groß darf mein Schema sein?

Die Web-UI begrenzt Uploads und Remote-Abrufe auf 10 MB. Die CLI hat
keine harte Grenze; das Parsen von OpenAPI erfolgt im Arbeitsspeicher.

## Schreibt ValIO in mein Repository zurück?

ValIO überschreibt niemals etwas außerhalb des Verzeichnisses, auf das
`--output` zeigt. Web- und Desktop-Apps schreiben für Client-Arbeit nie
auf die Festplatte des Servers — Web streamt ein ZIP-Archiv zurück in
den Browser, Desktop schreibt an einen Pfad, den Sie über einen nativen
Datei-Dialog auswählen.

## Ist der generierte Code kommerziell nutzbar?

Der generierte Code trägt keine Lizenzklauseln von ValIO; er übernimmt
die Lizenz, die Sie für Ihr Projekt wählen. ValIO selbst ist Open
Source — siehe `LICENSE` im Repository-Root.
