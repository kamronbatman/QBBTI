# QuickBooks Bank Transaction Importer (QBBTI)

A Windows desktop application that imports bank transactions from OFX/QFX/CSV files into QuickBooks Desktop. It auto-maps transactions to QuickBooks accounts using customizable rules, groups them for bulk review, and posts them via the QBXML SDK.

## Prerequisites

- **Windows 10/11**
- **[.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** (or SDK to build from source)
- **QuickBooks Desktop** (Pro, Premier, or Enterprise) installed and running with a company file open

## Getting Started

```bash
# Clone and build
git clone <repo-url>
cd QBBTI
dotnet build QBBTI.sln

# Run
dotnet run --project src/QBBTI.App
```

## How It Works

### 1. Import

Select a bank file and connect to QuickBooks. The app will prompt you to authorize access the first time. Choose the bank account the transactions belong to.

**Supported file formats:**

| Format | Extensions |
|--------|------------|
| OFX / QFX / QBO | `.ofx`, `.qfx`, `.qbo` |
| Chase CSV | `.csv` |

### 2. Review

Transactions are automatically grouped by matching mapping rules (or by payee if no rule matches). For each group you can:

- Edit the payee name and QuickBooks expense/income account
- Save a mapping rule so future imports auto-map the same way
- Ungroup individual transactions or bulk-ungroup with checkboxes
- Drag one group onto another to merge them
- Check/uncheck groups to include or exclude them from import

### 3. Import

Preview the generated QBXML for each transaction before confirming. If a payee doesn't exist in QuickBooks, you'll be prompted to Quick Add it as a Vendor, Customer, or Other Name.

## Mapping Rules

Rules are stored in `mappings.json` at the project root, scoped per QuickBooks company. Each rule matches against the raw transaction description and maps it to a payee, account, and entity type.

```json
{
  "Pattern": "GUSTO.*CO ENTRY",
  "IsRegex": true,
  "PayeeName": "Gusto",
  "AccountName": "Payroll Expenses",
  "EntityType": "Vendor",
  "Memo": "Payroll"
}
```

Rules can use substring matching (default) or regex. Higher-priority rules take precedence when multiple rules match.

## Project Structure

```
src/
  QBBTI.Core/          Core logic (no UI dependency)
    Parsing/           Bank file parsers (OFX, Chase CSV)
    Mapping/           Rule engine and transaction grouper
    QuickBooks/        QBXML builder and QB connection via COM
    Models/            Domain models
  QBBTI.App/           WPF desktop application
    Views/             XAML views
    ViewModels/        MVVM view models (CommunityToolkit.Mvvm)
    Services/          Dialog services
    Resources/         Styles and assets
```
