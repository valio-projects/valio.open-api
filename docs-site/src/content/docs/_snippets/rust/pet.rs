// src/models/pet.rs
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Pet {
    pub id: i64,
    pub name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub tag: Option<String>,
}

// src/client/pets_client.rs
use reqwest::{Client, Url};
use crate::models::pet::Pet;

#[derive(Debug, Clone)]
pub struct PetsClient {
    http: Client,
    base_url: Url,
}

impl PetsClient {
    pub fn new(http: Client, base_url: Url) -> Self {
        Self { http, base_url }
    }

    pub async fn list_pets(&self) -> reqwest::Result<Vec<Pet>> {
        self.http
            .get(self.base_url.join("/pets").expect("valid url"))
            .send()
            .await?
            .error_for_status()?
            .json::<Vec<Pet>>()
            .await
    }

    pub async fn create_pet(&self, body: &Pet) -> reqwest::Result<Pet> {
        self.http
            .post(self.base_url.join("/pets").expect("valid url"))
            .json(body)
            .send()
            .await?
            .error_for_status()?
            .json::<Pet>()
            .await
    }
}
