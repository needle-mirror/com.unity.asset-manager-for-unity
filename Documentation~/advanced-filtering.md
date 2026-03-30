# Using complex filters in the Asset Manager Window

### How to use filters to search for assets

---

The Asset Manager Window provides various filters to help you find assets. This document explains how to use advanced filters on asset's metadata in the `Assets` tab.

## Multi-Text Filters

Multi-text filters allow you to filter assets based on text values with advanced matching options. The following multi-text filters are available:

- **Name**: Filter assets by their name
- **Description**: Filter assets by their description

### Contains and Does Not Contain

Each filter value can be set to one of two modes:

- **Contains**: Include assets where the field contains the specified text
- **Does Not Contain**: Exclude assets where the field contains the specified text

### Wildcard Matching

Multi-text filters use wildcard matching for string comparisons. This means the filter will match any asset where the field contains the specified text anywhere within the value, not just exact matches.

For example, filtering with "tree" will match:
- "tree"
- "trees"
- "oak_tree_model"
- "MyTreeAsset"

### Adding Multiple Filter Values

You can add multiple filter values by clicking the **Add Value** button. Each value can independently be set to "Contains" or "Does Not Contain" mode.

### AND/OR Logic

When you have multiple filter values, you can choose how they are combined using the logic selector that appears between the first and subsequent filter values.

#### AND Logic (Default)

When AND logic is selected:
- For **Contains** values: Assets must match **all** specified values
- For **Does Not Contain** values: Assets are excluded if they match **all** specified exclusion values

**Example**: Name filter with AND logic
- Contains: "tree"
- Contains: "oak"

This returns assets where the name contains both "tree" AND "oak".

#### OR Logic

When OR logic is selected:
- For **Contains** values: Assets match if they contain **any** of the specified values
- For **Does Not Contain** values: Assets are excluded if they match **any** of the exclusion values

**Example**: Name filter with OR logic
- Contains: "tree"
- Contains: "bush"

This returns assets where the name contains either "tree" OR "bush".

> **Note**: When OR logic is selected, all Contains/Does Not Contain dropdowns are synchronized. Changing one will change all others to maintain consistent filter behavior.

### Combining Contains and Does Not Contain

You can combine both "Contains" and "Does Not Contain" values in the same filter:

**Example**: Name filter with AND logic
- Contains: "vegetation"
- Does Not Contain: "dead"

This returns assets where the name contains "vegetation" but does not contain "dead".

## File Extensions Filter

The file extensions filter allows you to filter assets based on their file extension (e.g., `.png`, `.fbx`, `.wav`).

### Available Extensions

The filter dropdown displays extensions found in your project's assets. However, due to API limitations, **not all file extensions may appear in the dropdown**. The available options depend on the number of assets in your project and the distribution of file types.

### Adding Custom Extensions

If the extension you're looking for doesn't appear in the dropdown, you can manually add it:

1. Click on the File Extensions filter to open the dropdown
2. Click the **Add value** row at the bottom of the list
3. A text field appears where you can type the desired extension (e.g., `.exr`)
4. Click the **Add** button to include it in your filter
5. The extension is added to the list and automatically selected

You can add multiple custom extensions by repeating this process. The dropdown remains open, allowing you to add several extensions in succession.

This allows you to filter by any extension, regardless of whether it appears in the predefined list.
