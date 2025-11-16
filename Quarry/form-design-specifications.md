# Nigerian Quarry Management System - Form Design Specifications

## Form Design Principles
1. **Nigerian Context**: All forms must accommodate Nigerian business practices, tax rates, and regulatory requirements
2. **Mobile Responsive**: Forms must work on tablets for weighbridge operations
3. **Real-time Validation**: Immediate feedback on data entry errors
4. **Role-based Access**: Different form views based on user roles
5. **Offline Capability**: Critical forms should work with intermittent connectivity

## Detailed Form Specifications

### 1. Customer Management Forms

#### Customer List Form
**URL**: `/Customer/Index`
**Access**: Admin, Manager, Accountant
**Features**:
- Searchable grid with columns: Name, Contact Person, Phone, Location, Credit Limit, Outstanding Balance, Status
- Filters: State, LGA, Status, Credit Limit Range
- Export to Excel functionality
- Quick actions: Edit, View Transactions, Generate Invoice

#### Add/Edit Customer Form
**URL**: `/Customer/Create` | `/Customer/Edit/{id}`
**Fields**:
```
Basic Information:
- Company Name* (text, max 100)
- RC Number (text, max 20) - Nigerian company registration
- Location* (text, max 255)
- LGA* (dropdown, Nigerian LGAs)
- State* (dropdown, Nigerian states)

Contact Details:
- Contact Person* (text, max 100)
- Phone* (text, max 20, Nigerian format validation)
- Email (email format validation, max 100)

Tax Information:
- TIN (text, max 20) - Tax Identification Number
- BVN (text, max 20) - Bank Verification Number

Financial:
- Billing Address (textarea, max 500)
- Credit Limit* (decimal, default 0)
- Payment Terms (dropdown: 30 days, 60 days, 90 days)

Status:
- Status* (dropdown: Active, Inactive, Blacklisted)
```

**Validation Rules**:
- Phone: Nigerian format (+234 or 0 prefix)
- Email: Standard email validation
- TIN: 10-12 digits if provided
- BVN: 11 digits if provided
- Credit Limit: Cannot be negative

### 2. Weighment Transaction Forms

#### New Weighment Form
**URL**: `/Weighment/Create`
**Process Flow**:
1. **Vehicle Information**
   - Vehicle Registration* (text, format: ABC-123-XYZ)
   - Driver Name (text, max 100)
   - Driver Phone (text, Nigerian format)

2. **Customer Selection**
   - Customer Dropdown* (searchable, shows credit status)
   - Display: Credit Limit, Outstanding Balance, Available Credit
   - Warning if credit limit exceeded

3. **Material Selection**
   - Material Dropdown* (shows current stock)
   - Unit Price (auto-populated, editable)
   - VAT Rate (auto-populated, default 7.5%)
   - Available Stock Display

4. **Weight Entry**
   - Weighbridge Selection* (dropdown of active weighbridges)
   - Gross Weight* (decimal, kg)
   - Tare Weight (decimal, kg, optional)
   - Net Weight (auto-calculated: Gross - Tare)
   - Weight Unit (dropdown: kg, tonnes)

5. **Calculation Section**
   - Subtotal (auto: Net Weight × Unit Price)
   - VAT Amount (auto: Subtotal × VAT Rate)
   - Total Amount (auto: Subtotal + VAT)
   - Price Per Unit confirmation

6. **Transaction Details**
   - Challan Number (auto-generated: format WB/NG/YYYY/###)
   - Entry Time (auto, editable)
   - Exit Time (auto, editable)
   - Operator* (auto-filled from logged-in user)
   - Notes (textarea)

**Real-time Features**:
- Credit check on customer selection
- Stock availability check
- Price validation
- Automatic calculations

#### Weighbridge Operations Dashboard
**URL**: `/Weighment/Operations`
**Features**:
- Real-time weight display from weighbridge
- Current vehicle on bridge
- Transaction queue
- Quick entry mode for high-volume operations
- Operator assignment

### 3. Material Management Forms

#### Material List Form
**URL**: `/Material/Index`
**Grid Columns**:
- Material Name
- Type (Aggregate, Sand, Dust, Laterite)
- Unit Price
- VAT Rate
- Current Stock
- Reserved Stock
- Available Stock
- Status

#### Material Price Update Form
**URL**: `/Material/Edit/{id}`
**Fields**:
- Unit Price* (decimal)
- VAT Rate* (decimal, default 7.5%)
- Effective Date* (date)
- Reason for Change (textarea)

### 4. Invoice Generation Forms

#### Generate Invoice Form
**URL**: `/Invoice/Create`
**Process**:
1. **Customer Selection**
   - Customer Dropdown* (shows unpaid weighments)
   - Display outstanding balance

2. **Weighment Selection**
   - Multi-select grid of completed weighments
   - Show: Date, Vehicle, Material, Net Weight, Amount
   - Filter by date range
   - Calculate running total

3. **Invoice Details**
   - Invoice Number (auto: INV/NG/YYYY/###)
   - Invoice Date* (default today)
   - Due Date* (based on customer payment terms)
   - Payment Terms* (dropdown)
   - LGA Receipt Number (for local government receipts)

4. **Calculation Summary**
   - Subtotal (sum of selected weighments)
   - VAT Amount (7.5% of subtotal)
   - Total Amount
   - In Words (auto-generated Nigerian format)

#### Invoice List Form
**URL**: `/Invoice/Index`
**Features**:
- Status tracking: Unpaid, Paid, Overdue
- Payment recording
- Print/PDF generation
- Email sending to customers

### 5. Employee/Payroll Forms

#### Employee Management Form
**URL**: `/Employee/Create` | `/Employee/Edit/{id}`
**Sections**:

**Personal Information**:
- Employee Code* (unique, format: EMP/###)
- Full Name* (max 200)
- Date of Birth* (date)
- Phone (Nigerian format)
- Email (email validation)

**Employment Details**:
- Department* (dropdown: Operations, Admin, Finance, Maintenance)
- Designation* (dropdown: Weighbridge Operator, Manager, Accountant, etc.)
- Date of Joining* (date)
- Status* (Active, Inactive, Terminated)

**Salary Structure**:
- Basic Salary* (decimal, monthly)
- Housing Allowance* (decimal)
- Transport Allowance* (decimal)
- Other Allowances (decimal)

**Nigerian Compliance**:
- Pension PIN (text, max 20)
- NHIS Number (text, max 20)
- Bank Name* (dropdown of Nigerian banks)
- Bank Account Number* (text, 10 digits)

#### Payroll Processing Form
**URL**: `/Payroll/Process`
**Features**:
- Select payment month
- Employee selection (multi-select or all)
- Automatic calculation:
  - Gross Pay (Basic + Allowances)
  - PAYE Tax (based on Nigerian tax brackets)
  - Pension Employee (8% of Basic + Housing)
  - Pension Employer (10% of Basic + Housing)
  - NHIS (5% of Basic, employer contribution)
  - NHF (2.5% of Basic)
- Net Pay calculation
- Payslip generation
- Bank payment file generation

### 6. Reporting Dashboard

#### Main Reports Dashboard
**URL**: `/Report/Index`
**Report Types**:
1. **Financial Reports**
   - Trial Balance (real-time)
   - Profit & Loss
   - Balance Sheet
   - Cash Flow

2. **Operational Reports**
   - Daily Weighment Summary
   - Customer-wise Sales
   - Material-wise Sales
   - Vehicle-wise Transactions

3. **Tax Reports**
   - VAT Input/Output Summary
   - PAYE Monthly Remittance
   - Pension Contribution Summary

4. **Stock Reports**
   - Current Stock Levels
   - Stock Movement
   - Reorder Alerts

## Form Validation Standards

### Common Validation Rules
1. **Required Fields**: Marked with asterisk (*)
2. **Nigerian Phone**: +234XXXXXXXXXX or 0XXXXXXXXXX
3. **Email**: Standard RFC format
4. **Dates**: Cannot be future dates for historical data
5. **Numbers**: Positive values only for quantities and amounts
6. **Text Length**: Enforce database field limits
7. **Dropdown Values**: Must be from predefined lists

### Business Rule Validation
1. **Credit Limit**: Warn if transaction exceeds available credit
2. **Stock Availability**: Prevent sales if insufficient stock
3. **Duplicate Prevention**: Check for duplicate invoices, challan numbers
4. **Date Logic**: Due date must be after invoice date
5. **Tax Calculations**: Automatic VAT at 7.5%, PAYE per tax tables

## User Experience Features
1. **Auto-save**: Draft saving for long forms
2. **Keyboard Navigation**: Tab order optimization
3. **Search-as-you-type**: For dropdowns with many options
4. **Bulk Operations**: Multi-select for invoices, payroll
5. **Print Optimization**: Forms optimized for printing
6. **Mobile Responsive**: Tablet-friendly for weighbridge operations
7. **Offline Support**: Critical forms work without internet
8. **Multi-language**: Support for local languages

## Security Considerations
1. **Role-based Access**: Different form views per role
2. **Audit Trail**: Log all changes with user and timestamp
3. **Data Encryption**: Sensitive data like BVN, bank accounts
4. **Input Sanitization**: Prevent SQL injection and XSS
5. **Rate Limiting**: Prevent form submission abuse
6. **CSRF Protection**: Built-in ASP.NET Core protection