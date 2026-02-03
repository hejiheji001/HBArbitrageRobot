# Visual Studio Launch Settings

This folder contains `launchSettings.json` which provides multiple startup profiles for the HBArbitrageRobot Client in Visual Studio.

## Available Profiles

### 1. Client (Development)
Default development profile with browser visible for debugging.
- Uses localhost:5000 for Server API
- Browser runs in non-headless mode (visible)
- Suitable for debugging and development

### 2. Client (Production - Headless)
Production-like profile with headless browser.
- Uses localhost:5000 for Server API
- Browser runs in headless mode (invisible)
- Suitable for testing production behavior

### 3. Client (Docker Server)
Profile for connecting to Server running in Docker.
- Uses localhost:8080 for Server API (Docker port)
- Browser runs in non-headless mode (visible)
- Suitable when Server is running via Docker Compose

### 4. Client (Command Line Args)
Profile using command-line arguments instead of environment variables.
- Demonstrates alternative configuration method
- Useful for understanding the argument order

## How to Use

1. **Open the solution** in Visual Studio
2. **Select a profile** from the dropdown next to the Start button
3. **Update credentials** in `launchSettings.json`:
   - Replace `your_username_here` with your touker.com username
   - Replace `your_password_here` with your touker.com password
4. **Press F5** or click Start to run with the selected profile

## Security Note

**IMPORTANT:** The `launchSettings.json` file contains placeholder credentials. You should:
- Replace placeholders with your actual credentials for local development
- **NEVER commit actual credentials** to version control
- Consider adding `launchSettings.json` to `.gitignore` if you customize it with real credentials

Alternatively, you can:
- Create a local `.env` file (already in `.gitignore`)
- Use User Secrets in Visual Studio (right-click project → Manage User Secrets)
- Keep placeholders in `launchSettings.json` and manually set credentials each time

## Configuration Options

Each profile supports these environment variables:

- **USERNAME**: Touker.com login username (required)
- **PASSWORD**: Touker.com login password (required)
- **API_URL**: Server API endpoint (default: http://localhost:5000)
- **PURCHASE_TIME**: Time for arbitrage orders in HH:mm format (default: 14:15)
- **MAX_INVESTMENT**: Max investment per stock holder per fund in yuan (default: 1000)
- **HEADLESS**: Run browser in headless mode, true or false (default: true)

## Example Customization

To customize for your needs, edit the environment variables in the profile:

```json
"Client (Development)": {
  "commandName": "Project",
  "environmentVariables": {
    "USERNAME": "myusername",
    "PASSWORD": "mypassword",
    "API_URL": "http://localhost:5290",
    "PURCHASE_TIME": "14:30",
    "MAX_INVESTMENT": "2000",
    "HEADLESS": "false"
  }
}
```
