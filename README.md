# Decision Support for Loans in a Bank Branch

## Project requirements

1. **Analyse the loan portfolio:** highlight the share of each loan type, such as consumer, auto and personal-needs loans, in the total loans granted to individuals.
2. **Identify debtors and payment problems:** highlight customers with overdue payments, defaulted loans or other warning statuses.
3. **Forecast lending volume:** estimate the volume of loans to be granted in the following period using historical lending and its trend.
4. **Simulate interest-rate changes:** estimate how demand for loans could change when interest rates decrease or increase.
5. **Evaluate profitability and cross-selling potential:** calculate estimated net margins across loan categories and identify low-risk customer profiles for complementary products, such as premium credit cards or life insurance. The intended analysis includes purchasing patterns where suitable data is available.
6. **Assess early repayment:** analyse customers who fully repay before maturity, estimate the interest forgone and suggest retention opportunities or alternative financial products for potentially liquid customers.

## The big picture

The application helps a bank branch review its lending activity and compare possible decisions. It brings portfolio composition, repayment problems, forecasts, profitability and customer opportunities into one desktop interface. It supports analysis; it does not approve loans or automatically contact customers.

The project uses the supplied Lending Club sample: **90,427 historical loan records**, with issue dates from **June 2007 to December 2018**. Amounts are in **US dollars**. Customer names are unavailable, so the application displays anonymized customer IDs.

The application works in four stages:

1. **Read the data:** a fixed SQL query joins loans, customer profiles, loan categories and loan statuses from the local SQLite database.
2. **Select the relevant loans:** the user chooses issue dates, a category and any additional filters for the current tab.
3. **Calculate the results:** C# code groups records, calculates indicators, applies recommendation rules and estimates forecasts or scenarios.
4. **Present the information:** charts show patterns and comparisons, while tables provide supporting results and individual details.

The database is opened read-only and loaded into memory. Changing filters updates the C# analysis; it does not generate dynamic SQL or change the database.

## Application tabs

### 1. Portfolio

Shows how lending is distributed across loan categories. The pie chart represents each category's share of the total granted amount, with smaller categories grouped together; the table also shows loan counts, amount shares, count shares and average interest rates. This helps identify which products account for most lending.

### 2. Debtors and arrears

Shows loans with payment problems or warning statuses, including grace period, late payments and charged-off loans. Users can filter by status or search by customer or loan ID, with the most severe cases listed first. Reported balances and closed-loan principal shortfalls are shown separately, because a charged-off loan can have a zero reported balance despite unrecovered principal.

### 3. Forecast

Estimates future monthly loan amounts and counts using a straight-line trend fitted to historical monthly lending. The user selects the history window and forecast horizon; the graph uses a solid blue line for recorded values and a dashed orange line for predictions, with the future period shaded. It helps assess the direction and scale of possible lending activity, rather than guaranteeing future results.

### 4. Interest rates

Compares lower, unchanged and higher interest-rate scenarios over the same forecast period. Each comparison panel shows the rate, estimated lending volume, expected loan count and demand change relative to the baseline. The user adjusts the rate change and demand elasticity; elasticity is an assumption because the source does not contain loan applications or rejected requests.

### 5. Profitability

Compares estimated lifetime net margins across loan categories using closed loans. The calculation includes interest, penalties, recoveries, principal losses and user-selected funding and operating costs; negative margins appear in red. The results help identify categories with stronger or weaker contributions under the selected assumptions, while row details show the financial breakdown.

### 6. Cross-selling

Suggests complementary products for profiles meeting the application's low-risk rules: grade A/B, no flagged status, zero recorded delinquencies and known debt-to-income ratio of at most 35%. The table places the suggested product and its reason first, with product, grade and ID filters to narrow the candidates. Suggestions use income, mortgage and card-related profile information; actual purchase histories and verified product ownership are unavailable, so this is rule-based targeting rather than market-basket analysis.

### 7. Early repayment

Shows fully paid loans completed at least three calendar months before their original maturity. The chart compares estimated unearned interest by loan category, while the table can switch between repayment impact and customer profiles, including months early and retention suggestions. Unearned interest is estimated contractual interest not received, not net profit lost; saved funding costs are not deducted from this figure.

### 8. Methods and data

Explains the source, calculations and main limitations of the analysis. Its chart shows the full dataset's loan count by issue year. Date and category filters are hidden because this tab describes the complete source rather than a selected portfolio.

## Using the application

- Use the tabs to select an analysis. Shared filters apply to the seven analysis tabs; click **Apply filters** after changing them.
- Use **Apply** for additional status, product, grade or search filters, and **Recalculate** after changing numerical assumptions.
- Tables display **50 results per page**. Use **Previous** and **Next**, or click a column heading to sort the complete result.
- Double-click a row or select **Details...** to see fields omitted from the main table.
- Choose **File → Export filtered results** to export all matching results, including detail columns, to CSV.
- Choose **Help → About this analysis...** for a longer explanation of the current screen.

## Running the project

1. Extract `Bank-Loan-Analysis-v3.zip`.
2. Open `Decision_support_loans_bank.sln` in Visual Studio with **.NET desktop development** and the **.NET 8 SDK** installed.
3. Allow NuGet packages to restore, then select the Windows Forms project as the startup project if necessary.
4. Press **F5**. The included `credite_banca.db` is copied to the output folder automatically.

Alternatively, run `Run-Windows.cmd` with the .NET 8 SDK installed. Python is only needed to rerun the original data-preparation scripts, not to use the application.

## Main source files

| File | Responsibility |
| --- | --- |
| `Program.cs` | Starts the Windows application. |
| `Form1.cs` | Builds the interface and coordinates filters, analyses, tables and export. |
| `Db.cs` | Opens SQLite and executes the read-only SQL query. |
| `Loan.cs` | Loads loan objects and provides repayment and margin calculations. |
| `Analytics.cs` | Aggregates monthly data, forecasts lending and applies scenario and product rules. |
| `Presentation.cs` | Provides English labels, action descriptions and result paging. |
| `BarChart.cs` | Draws the charts and scenario comparisons. |

## Interpretation limits

The dataset is a historical US sample, not a current Romanian bank portfolio. Issue-date filters select when loans were granted; they do not reconstruct loan statuses at that historical date. Forecasts describe the sample, interest-rate demand is a sensitivity scenario, profitability uses simplified cost assumptions, and cross-selling uses profile rules rather than observed purchases. Early-repayment dates have month-level precision, and income or early payoff alone does not prove that a customer currently holds surplus cash.
