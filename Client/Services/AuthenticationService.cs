using Microsoft.Playwright;

namespace Client.Services;

/// <summary>
/// Service for handling authentication to the trading platform
/// </summary>
public class AuthenticationService
{
    private readonly string _username;
    private readonly string _password;

    public AuthenticationService(string username, string password)
    {
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Perform login to the trading platform
    /// </summary>
    public async Task LogInAsync(IPage page)
    {
        Console.WriteLine("Attempting login...");

        try
        {
            // Wait for login form elements - based on actual DOM structure
            await page.WaitForSelectorAsync("input[type='tel'][placeholder='华宝证券客户号']", new PageWaitForSelectorOptions { Timeout = 10000 });

            // Fill in credentials using actual DOM selectors
            var usernameSelector = "input[type='tel'][placeholder='华宝证券客户号']";
            var passwordSelector = "input[type='tel'][max='6'].hide_input";
            var checkboxSelector = "div.check-input";
            var submitSelector = "button.hb-btn.hb-btn-primary:has-text('登录')";

            await page.FillAsync(usernameSelector, _username);
            await page.FillAsync(passwordSelector, _password);

            // Click the confirmation checkbox before submitting
            Console.WriteLine("Clicking confirmation checkbox...");
            await page.ClickAsync(checkboxSelector);

            // Wait a moment for the button to become enabled
            await Task.Delay(500);

            Console.WriteLine("Credentials entered, submitting...");
            await page.ClickAsync(submitSelector);

            // Wait for navigation after login
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Add additional wait to ensure session is fully established
            Console.WriteLine("Waiting for session to be established...");
            await Task.Delay(3000); // 3 seconds

            Console.WriteLine("Login submitted successfully");
            Console.WriteLine($"Current URL after login: {page.Url}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: Login selectors may need adjustment. Error: {ex.Message}");
            Console.WriteLine("Continuing to navigate to main page...");
        }
    }
}
