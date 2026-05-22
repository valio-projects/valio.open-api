// models/Pet/Pet.ts
export interface Pet {
  id: number;
  name: string;
  tag?: string;
}

// models/Pet/schemas/Pet.schema.ts
import { z } from "zod";

export const PetSchema = z.object({
  id: z.number().int(),
  name: z.string().min(1),
  tag: z.string().optional(),
});

export type PetFromSchema = z.infer<typeof PetSchema>;
