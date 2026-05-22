// api/listPets/listPets.fetch.ts
import { PetSchema } from "../../models/Pet/schemas/Pet.schema";
import type { Pet } from "../../models/Pet/Pet";

export async function listPets(init?: RequestInit): Promise<Pet[]> {
  const response = await fetch("/pets", {
    method: "GET",
    headers: { Accept: "application/json" },
    ...init,
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const body = await response.json();
  return PetSchema.array().parse(body);
}

// api/createPet/createPet.fetch.ts
export interface CreatePetParams {
  body: Pet;
}

export async function createPet(
  params: CreatePetParams,
  init?: RequestInit,
): Promise<Pet> {
  const response = await fetch("/pets", {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(params.body),
    ...init,
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return PetSchema.parse(await response.json());
}
