using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurGroceries.API;

public class API
{

}

// Custom exceptions
public class InvalidLoginException : Exception
{
    public InvalidLoginException(string message) : base(message) { }
    public InvalidLoginException(string message, Exception innerException) : base(message, innerException) { }
}

// DTOs for API responses
public class ListOverview
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string versionId { get; set; } = string.Empty;
    public int activeCount { get; set; } = 0;
}

public class ListItem
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string value { get; set; } = string.Empty;
    public string? categoryId { get; set; }
    public string? note { get; set; }
    public bool crossedOff { get; set; }
}

public class GroceryList
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string notes { get; set; } = string.Empty;
    public string externalListAccess { get; set; } = string.Empty;
    public string versionId { get; set; } = string.Empty;

    public string listType { get; set; } = string.Empty;
    public List<ListItem> items { get; set; } = new();
}

public class ListData
{
    public GroceryList list { get; set; } = new();
}

public class CategoryItem
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string value { get; set; } = string.Empty;
}


// Input models
public class NewListItem
{
    public string Value { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string? Note { get; set; }

    public NewListItem(string value, string? categoryId = null, string? note = null)
    {
        Value = value;
        CategoryId = categoryId;
        Note = note;
    }
}


// Main OurGroceries client class
public class OurGroceriesClient : IDisposable
{
    private readonly ILogger<OurGroceriesClient> _logger;
    private readonly HttpClient _httpClient;
    private HttpClientHandler _cookieHandler;

    private readonly string _username;
    private readonly string _password;

    private string? _sessionKey;
    private string? _teamId;
    private string? _masterListId;
    private string? _categoryId;

    // Constants
    private const string BaseUrl = "https://www.ourgroceries.com";
    private const string SignInUrl = BaseUrl + "/sign-in";
    private const string YourListsUrl = BaseUrl + "/your-lists/";

    private const string FormKeyUsername = "emailAddress";
    private const string FormKeyPassword = "password";
    private const string FormKeyAction = "action";
    private const string FormValueAction = "sign-in";

    // API Actions
    private const string ActionGetList = "getList";
    private const string ActionGetLists = "getOverview";
    private const string ActionItemCrossedOff = "setItemCrossedOff";
    private const string ActionItemAdd = "insertItem";
    private const string ActionItemAddItems = "insertItems";
    private const string ActionItemRemove = "deleteItem";
    private const string ActionItemRename = "changeItemValue";
    private const string ActionListCreate = "createList";
    private const string ActionListRemove = "deleteList";
    private const string ActionListRename = "renameList";
    private const string ActionGetMasterList = "getMasterList";
    private const string ActionGetCategoryList = "getCategoryList";
    private const string ActionItemChangeValue = "changeItemValue";
    private const string ActionListDeleteAllCrossedOff = "deleteAllCrossedOffItems";

    // Regex patterns
    private static readonly Regex TeamIdRegex = new(@"g_teamId = ""(.*)"";", RegexOptions.Compiled);
    private static readonly Regex StaticMetalistRegex = new(@"g_staticMetalist = (\[.*\]);", RegexOptions.Compiled);
    private static readonly Regex MasterListIdRegex = new(@"g_masterListUrl = ""/your-lists/list/(\S*)""", RegexOptions.Compiled);

    public OurGroceriesClient(string username, string password, ILogger<OurGroceriesClient>? logger = null)
    {
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OurGroceriesClient>.Instance;

        _cookieHandler = new HttpClientHandler()
        {
            CookieContainer = new CookieContainer()
        };

        _httpClient = new HttpClient(_cookieHandler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "OurGroceries.NET/1.0");
    }

    /// <summary>
    /// Logs into OurGroceries and initializes the session
    /// </summary>
    public async Task LoginAsync()
    {
        await GetSessionCookieAsync();
        await GetTeamIdAsync();
        await GetMasterListIdAsync();
        _logger.LogDebug("OurGroceries logged in successfully");
    }

    /// <summary>
    /// Gets all grocery lists for the user
    /// </summary>
    public async Task<List<ListOverview>> GetMyListsAsync()
    {
        _logger.LogDebug("Getting user lists");
        var response = await PostAsync(ActionGetLists);

        if (response.RootElement.TryGetProperty("shoppingLists", out var listsElement))
        {
            var json = listsElement.GetRawText();
            var lst = JsonSerializer.Deserialize<List<ListOverview>>(listsElement.GetRawText());
            return lst ?? new();
        }

        return new List<ListOverview>();
    }

    /// <summary>
    /// Gets category items
    /// </summary>
    public async Task<List<CategoryItem>> GetCategoryItemsAsync()
    {
        _logger.LogDebug("Getting category items");

        if (_categoryId == null)
            throw new InvalidOperationException("Category ID not initialized. Call LoginAsync first.");

        var payload = new Dictionary<string, object> { ["listId"] = _categoryId };
        var response = await PostAsync(ActionGetList, payload);

        if (response.RootElement.TryGetProperty("list", out var listElement) &&
            listElement.TryGetProperty("items", out var itemsElement))
        {
            var json = itemsElement.GetRawText();
            return JsonSerializer.Deserialize<List<CategoryItem>>(json) ?? new List<CategoryItem>();
        }

        return new List<CategoryItem>();
    }

    /// <summary>
    /// Gets items from a specific list
    /// </summary>
    public async Task<ListData> GetListItemsAsync(string listId)
    {
        _logger.LogDebug($"Getting items for list {listId}");

        var payload = new Dictionary<string, object> { ["listId"] = listId };
        var response = await PostAsync(ActionGetList, payload);

        //my change
        //var listData = JsonSerializer.Deserialize<ListData>(response.GetRawText()) ?? new ListData();
        var json = response.RootElement.GetRawText();

        var listData = JsonSerializer.Deserialize<ListData>(json) ?? new ListData();

        return listData;
    }

    /// <summary>
    /// Creates a new shopping list
    /// </summary>
    public async Task<String?> CreateListAsync(string name, string listType = "SHOPPING")
    {
        _logger.LogDebug($"Creating list: {name}");

        var payload = new Dictionary<string, object>
        {
            ["name"] = name,
            ["listType"] = listType.ToUpper()
        };

        var resp = await PostAsync(ActionListCreate, payload);       
        var listId = resp.RootElement.GetProperty("listId").GetString();
        return listId;
    }

    /// <summary>
    /// Creates a new category
    /// </summary>
    public async Task<JsonDocument> CreateCategoryAsync(string name)
    {
        _logger.LogDebug($"Creating category: {name}");

        if (_categoryId == null)
            throw new InvalidOperationException("Category ID not initialized. Call LoginAsync first.");

        var payload = new Dictionary<string, object>
        {
            ["value"] = name,
            ["listId"] = _categoryId
        };

        return await PostAsync(ActionItemAdd, payload);
    }

    /// <summary>
    /// Toggles an item's crossed off status
    /// </summary>
    public async Task<JsonDocument> ToggleItemCrossedOffAsync(string listId, string itemId, bool crossOff = false)
    {
        _logger.LogDebug($"Toggling item {itemId} crossed off status to {crossOff}");

        var payload = new Dictionary<string, object>
        {
            ["listId"] = listId,
            ["itemId"] = itemId,
            ["crossedOff"] = crossOff
        };

        return await PostAsync(ActionItemCrossedOff, payload);
    }

    /// <summary>
    /// Adds a single item to a list
    /// </summary>
    public async Task<JsonDocument> AddItemToListAsync(string listId, string value, string category = "uncategorized", bool autoCategory = false, string? note = null)
    {
        _logger.LogDebug($"Adding item '{value}' to list {listId}");

        var payload = new Dictionary<string, object>
        {
            ["listId"] = listId,
            ["value"] = value,
            ["note"] = note ?? string.Empty
        };

        if (!autoCategory)
        {
            payload["categoryId"] = category;
        }

        return await PostAsync(ActionItemAdd, payload);
    }

    /// <summary>
    /// Adds multiple items to a list
    /// </summary>
    public async Task<JsonDocument> AddItemsToListAsync(string listId, IEnumerable<NewListItem> items)
    {
        _logger.LogDebug($"Adding multiple items to list {listId}");

        var itemPayloads = items.Select(item => new Dictionary<string, object?>
        {
            ["listId"] = listId,
            ["value"] = item.Value,
            ["categoryId"] = item.CategoryId,
            ["note"] = item.Note
        }).ToList();

        var payload = new Dictionary<string, object>
        {
            ["items"] = itemPayloads
        };

        return await PostAsync(ActionItemAddItems, payload);
    }

    /// <summary>
    /// Removes an item from a list
    /// </summary>
    public async Task<JsonDocument> RemoveItemFromListAsync(string listId, string itemId)
    {
        _logger.LogDebug($"Removing item {itemId} from list {listId}");

        var payload = new Dictionary<string, object>
        {
            ["listId"] = listId,
            ["itemId"] = itemId
        };

        return await PostAsync(ActionItemRemove, payload);
    }

    /// <summary>
    /// Gets the master list
    /// </summary>
    public async Task<JsonDocument> GetMasterListAsync()
    {
        _logger.LogDebug("Getting master list");

        if (_masterListId == null)
            throw new InvalidOperationException("Master list ID not initialized. Call LoginAsync first.");

        var payload = new Dictionary<string, object> { ["listId"] = _masterListId };
        return await PostAsync(ActionGetList, payload);
    }

    /// <summary>
    /// Gets the category list
    /// </summary>
    public async Task<JsonDocument> GetCategoryListAsync()
    {
        _logger.LogDebug("Getting category list");

        if (_teamId == null)
            throw new InvalidOperationException("Team ID not initialized. Call LoginAsync first.");

        var payload = new Dictionary<string, object> { ["teamId"] = _teamId };
        return await PostAsync(ActionGetCategoryList, payload);
    }

    /// <summary>
    /// Deletes a list
    /// </summary>
    public async Task<JsonDocument> DeleteListAsync(string listId)
    {
        _logger.LogDebug($"Deleting list {listId}");

        if (_teamId == null)
            throw new InvalidOperationException("Team ID not initialized. Call LoginAsync first.");

        var payload = new Dictionary<string, object>
        {
            ["listId"] = listId,
            ["teamId"] = _teamId
        };

        return await PostAsync(ActionListRemove, payload);
    }

    /// <summary>
    /// Deletes all crossed off items from a list
    /// </summary>
    public async Task<JsonDocument> DeleteAllCrossedOffFromListAsync(string listId)
    {
        _logger.LogDebug($"Deleting all crossed off items from list {listId}");

        var payload = new Dictionary<string, object>
        {
            ["listId"] = listId
        };

        return await PostAsync(ActionListDeleteAllCrossedOff, payload);
    }

    /// <summary>
    /// Adds an item to the master list
    /// </summary>
    public async Task<JsonDocument> AddItemToMasterListAsync(string value, string categoryId)
    {
        _logger.LogDebug($"Adding item '{value}' to master list");

        if (_masterListId == null)
            throw new InvalidOperationException("Master list ID not initialized. Call LoginAsync first.");

        var payload = new Dictionary<string, object>
        {
            ["listId"] = _masterListId,
            ["value"] = value,
            ["categoryId"] = categoryId
        };

        return await PostAsync(ActionItemAdd, payload);
    }

    /// <summary>
    /// Changes an item on a list
    /// </summary>
    public async Task<JsonDocument> ChangeItemOnListAsync(string listId, string itemId, string categoryId, string value)
    {
        _logger.LogDebug($"Changing item {itemId} on list {listId}");

        if (_teamId == null)
            throw new InvalidOperationException("Team ID not initialized. Call LoginAsync first.");

        var payload = new Dictionary<string, object>
        {
            ["itemId"] = itemId,
            ["listId"] = listId,
            ["newValue"] = value,
            ["categoryId"] = categoryId,
            ["teamId"] = _teamId
        };

        return await PostAsync(ActionItemChangeValue, payload);
    }

    #region Private Methods

    private async Task GetSessionCookieAsync()
    {
        _logger.LogDebug("Getting session cookie");

        var formData = new List<KeyValuePair<string, string>>
            {
                new(FormKeyUsername, _username),
                new(FormKeyPassword, _password),
                new(FormKeyAction, FormValueAction)
            };

        using var content = new FormUrlEncodedContent(formData);
        using var response = await _httpClient.PostAsync(SignInUrl, content);

        var resp = await response.Content.ReadAsStringAsync();

        System.Diagnostics.Debug.WriteLine(_cookieHandler.CookieContainer.Count.ToString());

        foreach (Cookie cookie in _cookieHandler.CookieContainer.GetCookies(new Uri(BaseUrl)))
        {            
            _sessionKey = cookie.Value;
            _logger.LogDebug($"Found session key: {_sessionKey}");
        }

        if (string.IsNullOrEmpty(_sessionKey))
        {
            _logger.LogError("Could not find session cookie");
            throw new InvalidLoginException("Could not find session cookie");
        }
    }

    private async Task GetTeamIdAsync()
    {
        _logger.LogDebug("Getting team ID");

        _httpClient.DefaultRequestHeaders.Clear();
        //_httpClient.DefaultRequestHeaders.Add("Cookie", $"{CookieKeySession}={_sessionKey}");

        using var response = await _httpClient.GetAsync(YourListsUrl);
        var responseText = await response.Content.ReadAsStringAsync();

        var teamIdMatch = TeamIdRegex.Match(responseText);
        if (teamIdMatch.Success)
        {
            _teamId = teamIdMatch.Groups[1].Value;
            _logger.LogDebug($"Found team ID: {_teamId}");
        }
        else
        {
            throw new InvalidLoginException("Could not find team ID");
        }

        var staticMetalistMatch = StaticMetalistRegex.Match(responseText);
        if (staticMetalistMatch.Success)
        {
            var staticMetalistJson = staticMetalistMatch.Groups[1].Value;
            var staticMetalist = JsonSerializer.Deserialize<JsonElement[]>(staticMetalistJson);

            var categoryList = staticMetalist?.FirstOrDefault(list =>
                list.TryGetProperty("listType", out var listType) &&
                listType.GetString() == "CATEGORY");

            if (categoryList?.TryGetProperty("id", out var categoryId) == true)
            {
                _categoryId = categoryId.GetString();
                _logger.LogDebug($"Found category ID: {_categoryId}");
            }
        }
    }

    private async Task GetMasterListIdAsync()
    {
        _logger.LogDebug("Getting master list ID");

        using var response = await _httpClient.GetAsync(YourListsUrl);
        var responseText = await response.Content.ReadAsStringAsync();

        var masterListMatch = MasterListIdRegex.Match(responseText);
        if (masterListMatch.Success)
        {
            _masterListId = masterListMatch.Groups[1].Value;
            _logger.LogDebug($"Found master list ID: {_masterListId}");
        }
        else
        {
            throw new InvalidLoginException("Could not find master list ID");
        }
    }

    private async Task<JsonDocument> PostAsync(string command, Dictionary<string, object>? otherPayload = null)
    {
        if (string.IsNullOrEmpty(_sessionKey))
        {
            await LoginAsync();
        }

        var payload = new Dictionary<string, object> { ["command"] = command };

        if (!string.IsNullOrEmpty(_teamId))
        {
            payload["teamId"] = _teamId;
        }

        if (otherPayload != null)
        {
            foreach (var kvp in otherPayload)
            {
                payload[kvp.Key] = kvp.Value;
            }
        }

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();

        using var response = await _httpClient.PostAsync(YourListsUrl, content);
        var responseText = await response.Content.ReadAsStringAsync();

        return JsonDocument.Parse(responseText);
    }

    #endregion

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

}