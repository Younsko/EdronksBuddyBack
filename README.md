# Budget Buddy API v1

Expense tracking API for students worldwide.

Base URL: `/api/`

---

## Table of Contents
- [Auth](#auth)
  - [Register](#register)
  - [Login](#login)
  - [Validate Token](#validate-token)
- [Categories](#categories)
  - [Get All Categories](#get-all-categories)
  - [Create Category](#create-category)
  - [Get Category by ID](#get-category-by-id)
  - [Update Category](#update-category)
  - [Delete Category](#delete-category)
- [Transactions](#transactions)
  - [Get Transactions](#get-transactions)
  - [Create Transaction](#create-transaction)
  - [Get Transaction by ID](#get-transaction-by-id)
  - [Update Transaction](#update-transaction)
  - [Delete Transaction](#delete-transaction)
  - [Get Monthly Transactions](#get-monthly-transactions)
- [User](#user)
  - [Get User Profile](#get-user-profile)
  - [Update User Profile](#update-user-profile)
  - [Delete User Profile](#delete-user-profile)
  - [Get User Stats](#get-user-stats)

---

## Auth

### Register
**POST** `/auth/register`

**Request Body (JSON):**
```json
{
  "name": "string",
  "username": "string",
  "email": "user@example.com",
  "password": "string"
}
Response (200):


{
  "id": 0,
  "username": "string",
  "email": "string",
  "name": "string",
  "profilePhotoUrl": "string|null",
  "token": "string",
  "expiresAt": "2025-10-16T19:42:12.298Z"
}
Errors: 400 Bad Request (validation errors, email or username taken)

Login
POST /auth/login

Request Body (JSON):


{
  "username": "string",
  "password": "string"
}
Response (200):


{
  "id": 0,
  "username": "string",
  "email": "string",
  "name": "string",
  "profilePhotoUrl": "string|null",
  "token": "string",
  "expiresAt": "2025-10-16T19:42:12.301Z"
}
Errors: 401 Unauthorized

Validate Token
POST /auth/validate

Request Body: "string" (token)

Response (200):


{
  "valid": true
}
Categories
Get All Categories
GET /categories

Query Parameters:

year (optional, int)

month (optional, int)

Response (200):


[
  {
    "id": 0,
    "name": "string",
    "color": "string",
    "monthlyBudget": 0,
    "spentThisMonth": 0,
    "remainingBudget": 0,
    "transactionCount": 0
  }
]
Create Category
POST /categories

Request Body (JSON):


{
  "name": "string",
  "color": "#RRGGBB",
  "monthlyBudget": 1000
}
Response (201):


{
  "id": 0,
  "name": "string",
  "color": "string",
  "monthlyBudget": 0,
  "spentThisMonth": 0,
  "remainingBudget": 0,
  "transactionCount": 0
}
Errors: 400 Bad Request

Get Category by ID
GET /categories/{id}

Response (200):


{
  "id": 0,
  "name": "string",
  "color": "string",
  "monthlyBudget": 0,
  "spentThisMonth": 0,
  "remainingBudget": 0,
  "transactionCount": 0
}
Errors: 403 Forbidden, 404 Not Found

Update Category
PUT /categories/{id}

Request Body (JSON):


{
  "name": "string",
  "color": "#RRGGBB",
  "monthlyBudget": 1000
}
Response (200): Same as Get Category by ID

Errors: 403 Forbidden, 404 Not Found

Delete Category
DELETE /categories/{id}

Response (200): Success
Errors: 403 Forbidden, 404 Not Found

Transactions
Get Transactions
GET /transactions

Query Parameters:

page (optional, default 1)

pageSize (optional, default 20)

Response (200):


[
  {
    "id": 0,
    "categoryId": 0,
    "categoryName": "string",
    "categoryColor": "string",
    "amount": 0,
    "currency": "string",
    "description": "string",
    "receiptImageUrl": "string",
    "transactionDate": "2025-10-16T19:42:12.315Z",
    "createdAt": "2025-10-16T19:42:12.315Z"
  }
]
Create Transaction
POST /transactions

Request Body (JSON):


{
  "categoryId": 0,
  "amount": 100,
  "currency": "USD",
  "description": "string",
  "receiptImage": "string|null"
}
Response (201): Same as Get Transactions

Errors: 400 Bad Request

Get Transaction by ID
GET /transactions/{id}

Response (200): Same as Get Transactions
Errors: 403 Forbidden, 404 Not Found

Update Transaction
PUT /transactions/{id}

Request Body (JSON): Same as Create Transaction

Response (200): Same as Get Transactions
Errors: 403 Forbidden, 404 Not Found

Delete Transaction
DELETE /transactions/{id}

Response (200): Success
Errors: 403 Forbidden, 404 Not Found

Get Monthly Transactions
GET /transactions/month/{year}/{month}

Query Parameters: page (default 1), pageSize (default 20)

Response (200): Same as Get Transactions

User
Get User Profile
GET /user/profile

Response (200):


{
  "id": 0,
  "name": "string",
  "username": "string",
  "email": "string",
  "preferredCurrency": "string",
  "profilePhotoUrl": "string|null",
  "createdAt": "2025-10-16T19:42:12.333Z",
  "updatedAt": "2025-10-16T19:42:12.333Z",
  "totalCategories": 0,
  "totalTransactions": 0
}
Errors: 404 Not Found

Update User Profile
PUT /user/profile

Request Body (JSON):


{
  "name": "string",
  "email": "user@example.com",
  "password": "string",
  "preferredCurrency": "USD",
  "profilePhotoUrl": "string|null"
}
Response (200): Same as Get User Profile
Errors: 400 Bad Request, 404 Not Found

Delete User Profile
DELETE /user/profile

Request Body (JSON): "string" (confirmation?)

Response (200): Success
Errors: 404 Not Found

Get User Stats
GET /user/stats

Query Parameters:

year (optional, default 0)

month (optional, default 0)

Response (200):

{
  "totalSpentThisMonth": 0,
  "totalBudgetThisMonth": 0,
  "remainingBudget": 0,
  "budgetUsagePercentage": 0,
  "totalTransactions": 0,
  "byCategory": [
    {
      "categoryName": "string",
      "color": "string",
      "spent": 0,
      "budget": 0,
      "percentage": 0,
      "transactionCount": 0
    }
  ],
  "byCurrency": [
    {
      "currency": "string",
      "amount": 0,
      "convertedToPreferred": 0
    }
  ],
  "dailySpending": [
    {
      "date": "2025-10-16T19:42:12.339Z",
      "amount": 0,
      "transactionCount": 0
    }
  ]
}
Notes
Authentication: Bearer token via Authorization: Bearer <token> required for all endpoints except /auth/*.

profilePhotoUrl: Can be null. Display a default avatar on the frontend if null.

Pagination: Transactions endpoints support page and pageSize.

Currency: Always 3-letter code (USD, EUR, etc.).

Errors: Standard HTTP codes (400, 401, 403, 404, 500) with detail message.