# Interview Task: Caching Strategy & Consistency (.NET)

## Space: Engineering Hiring | Version: 1.0
**Level:** Senior .NET Developer  
**Focus:** Dependency Injection, Service Lifetimes, Concurrency & State  
**Tech Stack:** .NET 8, ASP.NET Core Web API  
**Allowed Tools:** Documentation, Google, Stack Overflow, AI tools  

---

## 🎯 Goal of the Assignment
This assignment evaluates your architectural and design skills. Business logic is intentionally simple, while **architecture, performance, and correctness** are the focus.

Specifically, this task evaluates your ability to:
* Design a proper caching layer
* Avoid common cache bugs
* Handle cache invalidation
* Prevent stale data issues
* Understand memory vs. distributed cache
* Apply correct lifetime & expiration strategy

---

## 🏢 Business Scenario (Minimal & Fixed)
You are implementing a **Product Catalog API** with the following endpoints:
* `GET /api/products/{id}` – Fetch a product by ID.
* `POST /api/products` – Create a new product.
* `PUT /api/products/{id}` – Update an existing product.

**Data Storage:** Products are stored in an in-memory repository using a standard collection (`Dictionary`).  
**Objective:** Improve read performance using an optimized caching layer.

---

## 🛠️ Mandatory Requirements

### Part 1 - Caching Layer
You must implement:
* A proper caching abstraction (e.g., `IProductCache`).
* Use `.NET`'s built-in `IMemoryCache` **OR** design a custom abstraction.
* **Apply caching only for GET requests.**

**Key Requirements:**
* The cache key generation must be **deterministic**.
* A clear cache expiration strategy must be defined.
* **Avoid caching null values accidentally** (this is your design decision to solve).

### Part 2 - Cache Invalidation
You must handle data consistency during state mutation events:
* **Product Creation** (`POST`)
* **Product Update** (`PUT`)

**Behavior Specifications:**
* The cache must **invalidate or refresh correctly** immediately upon mutation.
* Avoid stale reads (subsequent `GET` requests must strictly return the newest data).
* Must **not** require an application restart to take effect.
* Demonstrate correctness via a **simple test scenario**.

### Part 3 - Expiration Strategy
You must implement at least one of the following strategies and be ready to justify your choice during the oral defense:
* **Absolute expiration**
* **Sliding expiration**

---

## 🚀 Part 4 - Advanced Requirement (MANDATORY)
Choose and implement **one** of the following advanced architecture options, and be ready to justify your choice:

* **Option A: Cache Stampede Prevention**
  * Implement double-check locking or a `GetOrCreateAsync` pattern to guarantee that under high concurrent load, only a single thread requests data from the underlying repository simultaneously.
* **Option B: Distributed Cache Simulation**
  * Simulate a distributed cache (e.g., Redis) via an explicit interface abstraction layers.
* **Option C: Manual Cache Invalidation Endpoint**
  * Implement an administration or operational API endpoint (e.g., `POST /api/cache/clear`) to clear the cache layer manually.

---

## ❌ Explicitly Out of Scope
To save time, **do NOT implement** any of the following:
* Real Redis integration (optional interface abstraction layer only).
* Actual external Database connections.
* Third-party caching libraries.
* Complex data persistence.
* Background refresh workers or services.

*Focus strictly on the fundamentals.*

---

## 📤 Submission Requirements
Submit a link to a **public GitHub repository** (or shared directly with the interviewer) containing a zipped project folder.

The `README` file must include **ONLY**:
1. How to run the application.
2. Example request flow.
3. Example demonstrating a cache hit/miss scenario (e.g., execution latencies or console traces).
4. Brief notes on key design decisions.

*Note: No lengthy written architectural explanations are required. All deep design decisions will be covered comprehensively during the oral defense.*