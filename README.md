# OurGroceries.NET
This is a .net library to access the ourgroceries.com api

This is a fork of [https://github.com/ljmerza/py-our-groceries](https://github.com/ljmerza/py-our-groceries) (Thanks!)  
The python library was sent through an AI to convert it to c#.  
A few small errors were manually fixed.  
No human brain was strained.

Only limited testing has been done on the library, but it seems to function correctly. (Could probably do with some more error handling)

If you make any improvments, please submit a PR.

# Usage

```c#
// Create client
var client = new OurGroceriesClient("username", "password", logger);

// Login
await client.LoginAsync();

// Get all lists
var lists = await client.GetMyListsAsync();

// Add items to a list
await client.AddItemToListAsync(listId, "Milk", "dairy");

// Add multiple items
var items = new[]
{
    new NewListItem("Bread", "bakery"),
    new NewListItem("Apples", "produce", "Red apples")
};
await client.AddItemsToListAsync(listId, items);

// Toggle item as purchased
await client.ToggleItemCrossedOffAsync(listId, itemId, true);
```