
using OurGroceries.API;
using System.Linq;


var email = "*** someone@somewhere.com ***";
var password = "*** somepass ***";

var client = new OurGroceriesClient(email, password);

await client.LoginAsync();

await client.CreateListAsync("Test List");

var lists = await client.GetMyListsAsync();
var categories = await client.GetCategoryItemsAsync();

var test_list = lists.FirstOrDefault(l => l.name == "Test List");

var items = await client.GetListItemsAsync(test_list.id);

await client.AddItemToListAsync(test_list.id, "Milk");

Console.WriteLine(lists.Count.ToString());


