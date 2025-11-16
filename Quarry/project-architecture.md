# Nigerian Quarry Management System - ASP.NET Core Forms

## Project Overview
Building a comprehensive quarry management system with ASP.NET Core and SQL Server backend based on the provided database schema for Nigerian quarry operations.

## Technology Stack
- **Frontend**: ASP.NET Core MVC with Razor Pages
- **Backend**: C# .NET 8.0
- **Database**: SQL Server (as per provided schema)
- **ORM**: Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **UI Framework**: Bootstrap 5 with AdminLTE theme
- **Real-time**: SignalR for weighbridge operations

## Project Structure
```
QuarryManagementSystem/
├── Controllers/
│   ├── DashboardController.cs
│   ├── CustomerController.cs
│   ├── WeighmentController.cs
│   ├── MaterialController.cs
│   ├── InvoiceController.cs
│   ├── EmployeeController.cs
│   ├── PayrollController.cs
│   └── ReportController.cs
├── Models/
│   ├── Domain Models (from SQL schema)
│   ├── ViewModels/
│   └── DTOs/
├── Views/
│   ├── Shared/
│   ├── Dashboard/
│   ├── Customer/
│   ├── Weighment/
│   ├── Material/
│   ├── Invoice/
│   ├── Employee/
│   ├── Payroll/
│   └── Report/
├── Services/
│   ├── Interfaces/
│   ├── Implementations/
│   └── Database/
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── Repositories/
│   └── Migrations/
├── wwwroot/
│   ├── css/
│   ├── js/
│   ├── images/
│   └── lib/
└── appsettings.json
```

## Database Models (Entity Framework)

### Core Entities
1. **ChartOfAccounts** - Nigerian COA
2. **Quarries** - Company information
3. **Materials** - Aggregate types and pricing
4. **Customers** - Construction companies
5. **Employees** - Staff with Nigerian payroll details
6. **Weighbridges** - Weighbridge configuration
7. **WeighmentTransactions** - Core operational data
8. **Invoices** - Billing with VAT
9. **JournalEntries** - Double entry accounting
10. **PayrollRuns** - Monthly payroll processing

## Form Design Strategy

### 1. Main Dashboard
- Navigation menu with role-based access
- Quick stats: Daily weighments, pending invoices, stock levels
- Recent transactions and alerts
- Chart widgets for business metrics

### 2. Customer Management Forms
- **Customer List**: Searchable grid with filters
- **Add/Edit Customer**: 
  - Company details (Name, RCNumber, Location, LGA, State)
  - Contact information (ContactPerson, Phone, Email)
  - Tax details (TIN, BVN)
  - Credit limit and outstanding balance tracking

### 3. Weighment Transaction Forms
- **New Weighment**:
  - Vehicle registration and details
  - Customer selection with credit check
  - Material selection with current pricing
  - Weighbridge integration (manual entry or automated)
  - Automatic calculation: Net weight, subtotal, VAT, total
  - Challan number generation
- **Weighbridge Operations**:
  - Real-time weight display
  - Transaction status tracking
  - Operator assignment

### 4. Material Management Forms
- **Material List**: Current stock levels and pricing
- **Price Management**: Update unit prices and VAT rates
- **Stock Adjustment**: Manual stock corrections

### 5. Invoice Generation Forms
- **Generate Invoice**:
  - Select completed weighments
  - Automatic VAT calculation (7.5% Nigeria rate)
  - Payment terms selection
  - LGA receipt number tracking
- **Invoice List**: Track payment status

### 6. Employee/Payroll Forms
- **Employee Management**:
  - Personal details with Nigerian specifics (Pension PIN, NHIS)
  - Salary structure (Basic, Housing, Transport allowances)
  - Bank details for salary payment
- **Payroll Processing**:
  - Monthly payroll run
  - Automatic PAYE calculation
  - Pension and NHIS deductions
  - Payslip generation

### 7. Reporting Dashboard
- **Trial Balance**: Real-time from journal entries
- **Sales Reports**: Daily/Monthly/Customer wise
- **Stock Reports**: Current inventory levels
- **Payroll Reports**: Monthly summaries
- **VAT Reports**: Input/Output tax summaries

## Security & Access Control
- Role-based authentication (Admin, Manager, Operator, Accountant)
- User management with password policies
- Audit logging for all transactions
- Data encryption for sensitive information

## Integration Points
- Weighbridge hardware integration (serial/TCP/USB)
- Bank integration for salary payments
- Tax authority integration for VAT reporting
- SMS notifications for customers

## Implementation Phases

### Phase 1: Foundation
- Project setup and database connection
- Authentication system
- Main dashboard
- Customer management

### Phase 2: Core Operations
- Weighment transactions
- Material management
- Basic reporting

### Phase 3: Financial Management
- Invoice generation
- Journal entries
- Advanced reporting

### Phase 4: Payroll & Compliance
- Employee management
- Payroll processing
- Tax compliance features

## Database Connection Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=87.252.104.168;Database=QuarryManagementNG;User Id=sa;Password=*26malar19baby;TrustServerCertificate=true;"
  }
}
```

## Next Steps
1. Create ASP.NET Core project structure
2. Set up Entity Framework models
3. Implement authentication system
4. Build main dashboard
5. Create customer management forms
6. Implement weighment transaction forms
7. Add remaining modules sequentially