# 🏆 Wombastisches Elo Plugin 🏆  
**CounterStrikeSharp plugin to display Faceit Elo directly in-game!**  

## 📌 Requirements
For the plugin to work, the following addons are required:
- **[MetaMod](https://www.sourcemm.net/)** – A modding platform for CS2 servers
- **[CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)** – Enables C# plugins for CS2

Make sure these addons are up2date & installed correctly before proceeding with the plugin installation!

---

## 🚀 Features  
✅ Retrieve Elo of all connected players on the Server
✅ Retrieve a player's Faceit Stats using their Nickname  
✅ Direct integration with Counter-Strike 2 via Counter-StrikeSharp  
✅ Optimized API requests for fast responses  
✅ Debug mode for detailed error analysis  
✅ Custom permission group `@custom/faceit` for access control  
✅ Configurable visibility of Output (`self`, `admin`, or `all`)  

---

## 📥 Installation & Configuration  
### 1️⃣ **Download the latest release**  
Download the latest `.zip` file from the **[Releases](https://github.com/wombastisch/WombastischesEloPlugin/releases)** page.  

### 2️⃣ **Extract & Copy Files**  
Extract the ZIP file and copy its contents into the following directory on your CS2 server:  
csgo/addons/counterstrikesharp/plugins/


### 3️⃣ **Initial Setup & Debug Mode**  
After installation, ensure the plugin is properly loaded and configured:
- Run `css_plugins list` in the server console to check if the plugin is loaded.
- If not loaded, try `css_plugins load WombastischesEloPlugin`.
- If the plugin still does not appear, restart the server and check again.

### 4️⃣ **Configuration File**  
The plugin is configured via the following JSON configuration file:  
csgo/addons/counterstrikesharp/configs/plugins/WombastischesEloPlugin/config.json

Example configuration:
```json
{
  "DebugMode": false,
  "FaceitApiKey": "faceit-api-key-here",
  "RequiredPermissions": ["@custom/faceit", "@css/admin"],
  "OutputVisibility": "self"
}
```

### ⚙️ Configuration Options:

- **DebugMode:** Set to true to enable debug logs, which help diagnose issues. Set to `false` to disable debug logs.
- **FaceitApiKey:** Your Faceit API key. You can get it from [developers.faceit.com](https://developers.faceit.com) - *This is required!*
- **RequiredPermissions:** A list of permission groups that can use the `!faceit` command. Leave empty for unrestricted access or specify groups such as @css/admin.
- **OutputVisibility:** Defines who can see the Faceit Elo output.
    - **Options:**
        - `self` – Only the player who executes the command will see the results.
        - `admin` – Only admins with the correct permissions will see the results.
        - `all` – All players on the server will see the results.

## 🔑 Setting Up Permissions
By default, the plugin uses a custom permission group `@custom/faceit`, which must be defined in the `admins.json` file of Counter-StrikeSharp. Example:

```json
{
  "user1": {
    "identity": "steamID64 (Dec)",
    "flags": ["@css/root", "@custom/faceit"]
  },
  "user2": {
    "identity": "steamID64 (Dec)",
    "flags": ["@custom/faceit"]
  }
}
```
Save the file and restart the server to apply the changes.

## 🎮 Usage

Use the following command in the in-game chat or console to retrieve the Faceit Elo of all connected players:
```bash
!faceit
```
## ❓ Troubleshooting & Support

If you encounter issues:

- **Check server logs** – Errors such as invalid API keys or missing plugins will be logged. If not, check if "DebugMode" is set to `true` in the configuration file.
- **Is the plugin loaded?** – Use `"css_plugins list"` to verify.
- **Search GitHub Issues** – Someone might have already solved your issue: [GitHub Issues](https://github.com/wombastisch/WombastischesEloPlugin/issues)

If the problem persists, open a new issue with a detailed description of the problem.

---

## 🏗️ Development & Contributions

If you want to contribute:

1. **Clone the repository:**
    ```bash
    git clone https://github.com/wombastisch/WombastischesEloPlugin.git
    cd WombastischesEloPlugin
    ```

2. **Make changes & test**

3. **Submit a Pull Request**

## 📜 License

This project is licensed under the MIT License – free to use, modify, and distribute! 🎉
