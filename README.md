# PokeBridge

A RESTful API service that provides Pokemon information with fun translations (Shakespeare and Yoda styles).

## Features

- Fetch Pokemon species information from PokeAPI
- Automatic translation selection:
  - **Yoda translation**: For legendary Pokemon or cave-dwelling Pokemon
  - **Shakespeare translation**: For all other Pokemon
- Multi-layer caching with FusionCache (Memory + Redis)
- PostgreSQL persistence for Pokemon data and translations

## API Endpoints

### Get Pokemon Information
```http
GET /Pokemon/{name}
```
Returns basic Pokemon information without translation.

**Example:**
```bash
curl https://localhost:7111/Pokemon/pikachu
```

**Response:**
```json
{
  "name": "Pikachu",
  "description": "When several of these Pokémon gather, their electricity could build and cause lightning storms.",
  "habitat": "forest",
  "isLegendary": false
}
```

### Get Pokemon with Translation
```http
GET /Pokemon/translated/{name}
```
Returns Pokemon information with translated description based on its characteristics.

**Translation Rules:**
- **Legendary Pokemon** → Yoda translation
- **Cave habitat Pokemon** → Yoda translation
- **Other Pokemon** → Shakespeare translation

**Example:**
```bash
curl https://localhost:7111/Pokemon/translated/mewtwo
```

**Response:**
```json
{
  "name": "Mewtwo",
  "description": "Created by, a scientist was it, after years of horrific gene splicing and DNA engineering experiments.",
  "habitat": "rare",
  "isLegendary": true
}
```

## Rate Limiting

When the rate limit is exceeded (HTTP 429), the application automatically:
1. Logs a warning about the rate limit
2. Returns the **original Pokemon description** instead of failing
3. Caches the Pokemon data for future requests


## Caching Strategy

The api uses a multi-layer caching approach with FusionCache:

### Layer 1: Memory Cache (L1)
- **Duration**: 5 minutes
- **Scope**: Single application instance
- **Purpose**: Ultra-fast in-memory access

### Layer 2: Distributed Cache (L2 - Redis)
- **Duration**: 1 hour
- **Scope**: Shared across all application instances
- **Purpose**: Reduce database and external API calls

### Layer 3: Database (PostgreSQL)
- **Duration**: Permanent
- **Scope**: Persistent storage
- **Purpose**: Store Pokemon data and translations permanently

### Cache Invalidation
- Cache is invalidated when Pokemon data is updated (e.g., new translation added)
- [FusionCache Redis backplane](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md) notifies all instances about cache invalidation

## Running Locally

### Prerequisites
- .NET 9.0 SDK
- Docker (for PostgreSQL and Redis containers)

### Setup

1. Start infrastructure containers:
```bash
docker-compose up -d
```
The `docker-compose.yml` file includes also PgAdmin for easy DB management, and Redis Commander for Redis inspection.

PgAdmin: http://localhost:5050/browser/
Redis Commander: http://localhost:8081/

3. Run the application:
```bash
dotnet run --project src/PokeBridge.Api
```
> the application will run the migrations automatically on startup.

4. Access the API:
- Swagger UI: https://localhost:7111/swagger
- HTTP file: `src/PokeBridge.Api/PokeBridge.Api.http`

### Running Tests

Run all tests:
```bash
dotnet test
```

>  Make sure to have Docker up and running for integration tests.

## Project Structure

* PokeBridge.Api - REST API layer
* PokeBridge.Core - Domain logic & business rules
* PokeBridge.Infrastructure - External dependencies (DB, HTTP clients, caching)   
