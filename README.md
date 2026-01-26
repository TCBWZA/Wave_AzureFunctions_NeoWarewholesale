# Module 7: Azure Functions Integration - NeoWarewholesale

## Overview

This repository contains the **supporting materials for Module 7** of the Azure Functions training course. It demonstrates a complete .NET 8 Web API with Azure Functions integration patterns for processing orders from multiple external suppliers.

---

## 📚 Module 7 Learning Objectives

By completing this module, students will learn:

1. **API Development** - Building RESTful APIs with .NET 8
2. **Supplier Integration** - Handling multiple data formats from external systems
3. **Data Transformation** - Converting between supplier-specific and internal formats
4. **Azure Functions** - Creating HTTP-triggered functions in an isolated worker model
5. **Testing** - Writing comprehensive unit tests with Moq and NUnit
6. **Entity Framework Core** - Database operations with Code First approach

---

## 🏗️ Project Structure

This solution contains:

### **NeoWarewholesale.API** - Main Web API Project
A complete .NET 8 Web API demonstrating:
- RESTful API design
- Entity Framework Core 8 with SQL Server
- Repository pattern for data access
- Multiple supplier integration (Speedy, Vault)
- External order processing endpoints
- Automatic database seeding

### **NeoWarewholesale.Tests** - Test Suite
Comprehensive unit tests covering:
- 70 tests across mapping, controller, and model layers
- Mock-based testing (no in-memory database)
- Production-ready test patterns

---

## 📖 Documentation

The repository includes extensive documentation for students:

### **Getting Started**
- [API README.md](NeoWarewholesale.API/README.md) - Complete API setup and usage guide

### **External Integration**
- [EXTERNAL_ORDERS_README.md](NeoWarewholesale.API/EXTERNAL_ORDERS_README.md) - Supplier webhook integration overview
- [ENDPOINTS_SUMMARY.md](NeoWarewholesale.API/ENDPOINTS_SUMMARY.md) - Detailed endpoint documentation

### **Azure Functions**
- [AZURE_FUNCTIONS_HTTP_ONLY.md](NeoWarewholesale.API/AZURE_FUNCTIONS_HTTP_ONLY.md) - HTTP-only integration pattern
- [AZURE_FUNCTIONS_LAB_INSTRUCTIONS.md](NeoWarewholesale.API/AZURE_FUNCTIONS_LAB_INSTRUCTIONS.md) - **Student lab exercises**
- [AZURE_FUNCTIONS_EXAMPLES.md](NeoWarewholesale.API/AZURE_FUNCTIONS_EXAMPLES.md) - Complete function implementations

### **Additional Resources**
- [NAMESPACE_CONSIDERATIONS.md](NeoWarewholesale.API/NAMESPACE_CONSIDERATIONS.md) - Important namespace guidance for Azure Functions
- [Tests README.md](NeoWarewholesale.Tests/README.md) - Test suite documentation
- [TEST_SUITE_SUMMARY.md](NeoWarewholesale.Tests/TEST_SUITE_SUMMARY.md) - Test coverage summary

---

## 🎯 Assignment Overview

### **Scenario**
You work for **NeoWarehouse**, a wholesale company that receives orders from two suppliers:

1. **Speedy** - Uses numeric IDs and ISO timestamps
2. **Vault** - Uses email addresses, GUIDs, and Unix timestamps

Each supplier sends orders in their own format. Your task is to create Azure Functions that:
- Accept supplier-specific formats
- Transform data to internal format
- Validate customer and product references
- Save orders to the database via the API

### **Key Concepts Demonstrated**

#### **1. Data Format Transformation**
Learn to map between different data structures:
- `CustomerId` (long) ↔ `CustomerEmail` (string)
- `ProductId` (long) ↔ `ProductCode` (Guid)
- DateTime ↔ Unix timestamp
- Different address structures

#### **2. Integration Patterns**
Two approaches demonstrated:
- **Transform Only** - Data conversion without persistence
- **Full Integration** - Validation + transformation + database save

#### **3. Azure Functions (.NET 8 Isolated Worker)**
Modern serverless patterns:
- Non-static classes with dependency injection
- `HttpRequestData` and `HttpResponseData`
- System.Text.Json serialization
- Constructor injection of IHttpClientFactory

---

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or VS Code
- SQL Server (LocalDB or Express)
- Azure Functions Core Tools (for Functions development)

### Setup

1. **Clone the repository**
   ```powershell
   git clone https://github.com/TCBWZA/Wave_AzureFunctions_NeoWarewholesale
   cd Wave_AzureFunctions_NeoWarewholesale
   ```

2. **Set up the API**
   ```powershell
   cd NeoWarewholesale.API
   
   # Install EF Core tools locally
   dotnet new tool-manifest
   dotnet tool install dotnet-ef --local
   
   # Apply migrations
   dotnet tool run dotnet-ef database update
   
   # Run the API
   dotnet run
   ```

3. **Verify database seeding**
   The database automatically seeds with:
   - ✅ 2 Suppliers (Speedy, Vault)
   - ✅ 10 Customers
   - ✅ 20 Products
   - ✅ 5 Sample orders

4. **Run tests**
   ```powershell
   cd ../NeoWarewholesale.Tests
   dotnet test
   ```

---

## 📝 Assignment Tasks

### **Task 1: Understanding the API**
1. Review the API endpoints in [ENDPOINTS_SUMMARY.md](NeoWarewholesale.API/ENDPOINTS_SUMMARY.md)
2. Test the transform-only endpoints (`/fromspeedy`, `/fromvault`)
3. Understand the mapping logic in `Mappings/ExternalOrderMappingExtensions.cs`

### **Task 2: Create Speedy Azure Function**
Follow [AZURE_FUNCTIONS_LAB_INSTRUCTIONS.md](NeoWarewholesale.API/AZURE_FUNCTIONS_LAB_INSTRUCTIONS.md) to:
1. Create a new Azure Functions project (.NET 8 isolated)
2. Copy required DTOs (watch for namespace considerations!)
3. Implement the HTTP trigger function
4. Call the main API via HTTP
5. Test locally

### **Task 3: Create Vault Azure Function**
1. Create a function to handle Vault's format
2. Handle ProductCode → ProductId resolution
3. Convert Unix timestamps
4. Test with sample data

### **Task 4: Testing and Validation**
1. Test both functions locally
2. Verify orders are created in the database
3. Check logging output
4. Handle error scenarios

## 🏆 Success Criteria

Students successfully complete the module when they can:

✅ Explain the difference between in-process and isolated worker models  
✅ Create Azure Functions with proper dependency injection  
✅ Transform data between different supplier formats  
✅ Call a REST API from an Azure Function using HttpClient  
✅ Handle errors gracefully with appropriate HTTP status codes  
✅ Write and run unit tests for their functions  
✅ Understand when to use Azure Functions vs. direct API integration  

---

## 🛠️ Technologies Used

- **.NET 8** - LTS version
- **C# 12** - Language features
- **ASP.NET Core Web API** - RESTful services
- **Azure Functions (Isolated Worker)** - Serverless compute
- **Entity Framework Core 8** - ORM and migrations
- **SQL Server** - Database
- **NUnit** - Testing framework
- **Moq** - Mocking library
- **System.Text.Json** - JSON serialization

---

## 📞 Support

### For Students:
1. Read the comprehensive documentation in each folder
2. Check the troubleshooting sections in README files
3. Review the test suite for implementation examples
4. Consult with your instructor

### General Instructions:
- Sample implementations provided in AZURE_FUNCTIONS_EXAMPLES.md (Not yet released.)
- Test suite demonstrates best practices
- Common pitfalls documented in NAMESPACE_CONSIDERATIONS.md

---

## 📊 Assessment

This module includes:
- **Practical Labs** - Hands-on Azure Functions development
- **Testing** - Students should test their functions
- **Documentation** - Students document their design decisions in comments

---

**Module:** 7 - Azure Functions Integration  
**Course:** Azure Functions Development with .NET 8  

**Ready to start?** Head to [NeoWarewholesale.API/README.md](NeoWarewholesale.API/README.md) for setup instructions!
