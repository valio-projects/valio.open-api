# models/pet.py
from pydantic import BaseModel, ConfigDict, Field


class Pet(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    id: int = Field(..., alias="id")
    name: str = Field(..., min_length=1, alias="name")
    tag: str | None = Field(default=None, alias="tag")


# client/pets_client.py
from typing import Sequence

import httpx

from .models.pet import Pet


class PetsClient:
    def __init__(self, client: httpx.AsyncClient) -> None:
        self._client = client

    async def list_pets(self) -> Sequence[Pet]:
        response = await self._client.get("/pets")
        response.raise_for_status()
        return [Pet.model_validate(item) for item in response.json()]

    async def create_pet(self, body: Pet) -> Pet:
        response = await self._client.post(
            "/pets",
            json=body.model_dump(by_alias=True, exclude_none=True),
        )
        response.raise_for_status()
        return Pet.model_validate(response.json())
