# Decision Support for Loans in a Bank Branch


## Project requirements

1. **Analyse the loan portfolio:** highlight the share of each loan type, such as consumer, auto and personal-needs loans, in the total loans granted to individuals.
2. **Identify debtors and payment problems:** highlight customers with overdue payments, defaulted loans or other warning statuses.
3. **Forecast lending volume:** estimate the volume of loans to be granted in the following period using historical lending and its trend.
4. **Simulate interest-rate changes:** estimate how demand for loans could change when interest rates decrease or increase.
5. **Evaluate profitability and cross-selling potential:** calculate estimated net margins across loan categories and identify low-risk customer profiles for complementary products, such as premium credit cards or life insurance. The intended analysis includes purchasing patterns where suitable data is available.
6. **Assess early repayment:** analyse customers who fully repay before maturity, estimate the interest forgone and suggest retention opportunities or alternative financial products for potentially liquid customers.

# Our Team
- CHELARU I LAVINIA-BIANCA
- CORNICIUC V SILVIU-LUCIAN
- IOVU CI IASMINA-IULIANA

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

# Homework 2 — Bank Loan Decision Support

## Requirements
For Homework 2, we focused on spreadsheet exports, descriptive and predictive analysis, maps and cartodiagrams, interactive filters. We kept the loan analyses from Homework 1 and added ways to explore and present their results.

## What we added

We wanted to make the results easier to compare, share and explain. We focused on three parts:

1. **The state loans map**, shown in the **Geography** tab.
2. **The Excel exporter**, which saves a complete report or the current table.
3. **The map exporters**, which save the geographical analysis as HTML or PNG.

We used the existing historical Lending Club sample. The amounts are in USD, and the locations are US states. Customer names are not available, so we kept anonymized customer IDs.

## 1. State loans map — Geography tab

We added this tab to answer three questions: **Where did we grant more money? Where is the share of problematic loans higher? What types of loans are more common in each state?**

We grouped the selected loans by state. For each state, we calculated the loan count, total granted amount, flagged-loan count and flagged rate. The table below the map gives the exact values.

### Three map views

| Map view | What we show | Why we chose it |
| --- | --- | --- |
| Flagged-loan rate | States are shaded from light to dark blue. Darker states have a higher flagged rate. | We can compare the proportion of problematic loans across states with different loan counts. |
| Loan volume | Larger circles represent larger total granted amounts. | We can quickly see where lending is concentrated. |
| Loan-category pies | Pies show Auto, Debt consolidation and all other categories together. The slices represent amounts. | We can compare the loan mix within each state. |

The category pies show proportions, not total lending volume. We use smaller pies for some small states to reduce overlap. Gray states have no loans in the selected data. Alaska and Hawaii are shown separately as insets.

### How we calculated the flagged rate

**Flagged rate = flagged loans in the state / all selected loans in that state × 100**

For example, 20 flagged loans out of 200 selected loans means a flagged rate of **10%**. Flagged includes the source's payment-warning statuses, including grace-period cases; it does not mean that every flagged loan is a confirmed loss.

### How we interact with it

We can filter by issue dates, loan category, US state and individuals only. We can change the map view, hover over a state for values or click a state to select it. We choose **All states** to clear the state selection.

We chose an offline map because it works without a map account, API key or online map service. We included the state boundaries in the project.

**Decision supported:** we can identify states that deserve a closer risk review and compare lending concentration and product mix. These are historical sample results, so they do not prove that a state's entire population has the same risk.

## 2. Excel exporter

We added two separate Excel exports because a full analysis and a table of selected records serve different purposes.

### Export complete report

We choose **File → Export complete report (.xlsx)**.

We use only the shared filters: issue dates, category, state and individuals only. The complete report uses the same selection regardless of which tab is open. A status, product, risk-grade or ID filter from an individual tab does not narrow this report.

We made this choice so that exporting from Debtors does not turn the whole report into an analysis of flagged loans only. For example, the geographical flagged rate is still calculated against all loans in the shared selection.

| Sheet | What we included |
| --- | --- |
| Portfolio | Loan counts, granted amounts, category shares and average rates, with a pie chart. |
| Forecast | Recorded and forecast monthly amounts and counts, with a line chart and historical fit. |
| Interest scenarios | Lower, unchanged and higher interest rates, with estimated demand and lending. |
| Profitability | Funded amounts, assumed costs, net margins and lifetime margin percentages. |
| Geography | State counts, amounts and flagged rates, with a top-states chart. |
| Debtors and arrears | A summary by payment status, including balance, shortfall and review action. |
| Cross-selling | Eligible profiles grouped by suggested product. |
| Early repayment | Estimated unearned interest by category and average months saved. |
| Settings | The selected scope and assumptions we can edit. |
| Technical | Hidden calculation details and chart helper values. We did not include executed SQL. |

### We exported working analysis, not just values

We included Excel formulas, five native charts, filter buttons, readable headers and consistent number formats. We used conditional colours for the profitability margin:

- **Red:** below the lower threshold, initially 0%.
- **Amber:** from the lower threshold to below the upper threshold.
- **Green:** at or above the upper threshold, initially 5%.

We can change these thresholds through **Options → Profit margin colour thresholds** or in the exported Settings sheet. They change the colours, not the calculated profit.

The blue Settings cells also let us change funding cost, operating cost, rate-change size and demand elasticity. The linked formulas recalculate in Excel. To change the selected loans, history window or forecast horizon, we generate a new report from the application.

For the forecast, we used a straight-line trend. The solid line shows historical lending and the dashed line shows the forecast. **Historical fit (R²)** tells us how closely the trend describes past monthly amounts; it is not a percentage of forecast accuracy.

### Export current table

We choose **File → Export current table (.xlsx)**.

We export just the current table in one worksheet. We keep its specific filters, visible columns and sort order. We include all matching rows across every page, not only the 50 rows currently displayed. We leave out hidden detail columns and unrelated analysis sheets.

**Decision supported:** the complete report helps us compare the portfolio and test assumptions. The current-table export gives us a focused list to review, such as debtors or cross-selling candidates.

## 3. Map exporters

We kept two formats because we need both a report we can explore and an image we can place in a presentation.

| Export | What we save | When we use it |
| --- | --- | --- |
| Export geographical report (.html) | A standalone page with the selected map view, filter description, legend, hover values and a state summary table. | We open it in a browser to review and share the geographical results. |
| Export geographical map (.png) | A 1800 × 1150 image with the map, legend, filter description and source notes. | We insert it into slides or written reports. |

We use the shared filters for both geographical exports, regardless of the active tab. We choose the map representation in Geography before exporting.

We generated the HTML map using **SVG**, which stores shapes and text inside the page. We chose this approach so the report works offline without loading external scripts or map services. The HTML supports hover information; the PNG is a static image.

## Where we find the code

All the files below are in `Decision_support_loans_bank/`.

| File | What we did there |
| --- | --- |
| `Form1.cs` | We added the tabs, filters and File menu actions. |
| `Form1.Homework2.cs` | We connected the map and export buttons to the data. `CaptureCompleteReport` prepares the full report; `ExportCurrentTable` takes the visible table columns. |
| `LoanRepository.cs` | We built optional SQL conditions from the selected filters and bound user values as parameters. `ForCompleteReport` keeps only the shared filters. |
| `GeographyService.cs` | We grouped loans by state in `Aggregate` and loaded the boundary shapes. |
| `MapControl.cs` | We drew the Windows Forms map in `PaintMap`, handled map interaction and generated the PNG through `ExportPng`. |
| `MapHtmlExporter.cs` | We built the standalone SVG map and HTML summary table in `Write`. |
| `ExcelExporter.cs` | We created the full workbook in `Write` and the separate table workbook in `WriteCurrentTable`. |
| `Resources/us-states.geojson` | We stored the state boundary coordinates used by both map formats. |

We wrote the `.xlsx` files using Office Open XML: XML files packaged together in a ZIP-based workbook. This lets us generate real Excel formulas and charts without automating an installed copy of Excel.

## A simple presentation order

1. **We start with Geography.** We explain the three map views and show one state in the table.
2. **We apply a filter.** We show how the map and state values change.
3. **We export HTML and PNG.** We open the HTML report and explain why the PNG is useful for slides.
4. **We export the complete Excel report.** We show the forecast and profitability sheets, then change a blue cost assumption to demonstrate recalculation.
5. **We export the current Debtors table.** We explain that this file contains only the focused table, while the complete report still uses the shared selection.

We checked the calculations and exports with 75 automated checks, including workbook structure, shared-filter selection and current-table rows. We also checked spreadsheet recalculation and rendered previews. We still need to run the Windows interface and inspect the exports in the installed Excel version before presenting.
