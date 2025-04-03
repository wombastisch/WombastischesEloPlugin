# 🏆 Wombastic Elo Plugin 🏆  
**Counter-StrikeSharp plugin to display Faceit Elo directly in-game!**  

## 📌 Requirements
For the plugin to work, the following extensions are required:
- **[MetaMod](https://www.sourcemm.net/)** – A modding platform for CS2 servers
- **[Counter-StrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)** – Enables C# plugins for CS2

Make sure these extensions are installed before proceeding with the plugin installation!

---

## 🚀 Features  
✅ Retrieve a player's Faceit Elo using their SteamID  
✅ Direct integration with Counter-Strike 2 via Counter-StrikeSharp  
✅ Optimized API requests for fast responses  
✅ Debug mode for detailed error analysis  
✅ Custom permission group `@custom/faceit` for access control  

---

## 📥 Installation & Configuration  
### 1️⃣ **Download the latest release**  
Download the latest `.zip` file from the **[Releases](https://github.com/wombastisch/WombastischesEloPlugin/releases)** page.  

### 2️⃣ **Extract & Copy Files**  
Extract the ZIP file and copy its contents into the following directory on your CS2 server:  
```
csgo/counterstrikesharp/plugins/
```

### 3️⃣ **Initial Setup & Debug Mode**  
After installation, ensure the plugin is properly loaded and configured:
- Run `"css_plugins list"` in the server console to check if the plugin is loaded.
- If not loaded, try `"css_plugins load WombastischesEloPlugin"`.
- If the plugin still does not appear, restart the server and check again.

Configuration is done through the following file:
```
csgo/addons/counterstrikesharp/configs/plugins/WombastischesEloPlugin/WombastischesEloPlugin.json
```
Example configuration:
```json
{
  "DebugMode": true,
  "FaceitApiKey (Get Faceit API key: https://developer.faceit.com)": "faceit-api-key-here",
  "RequiredPermission": "@custom/faceit"
}
```

### 🔑 Setting Up Permissions
The plugin uses a custom permission group `@custom/faceit`, which must be defined in the `admins.json` file of Counter-StrikeSharp. Example:
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

---

## 🎮 Usage  
Use the following command in the in-game chat or console to retrieve the Faceit Elo of all connected players:  
```bash
!faceit
```

---

## ❓ Troubleshooting & Support  
If you encounter issues:
1. **Check server logs** – Errors such as invalid API keys or missing plugins will be logged. If not check if "DebugMode" is set to true in the configuration file.
2. **Is the plugin loaded?** – Use `"css_plugins list"` to verify.
3. **Search GitHub Issues** – Someone might have already solved your issue: [GitHub Issues](https://github.com/wombastisch/WombastischesEloPlugin/issues)

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

---

## 📜 License  
This project is licensed under the **MIT License** – free to use, modify, and distribute! 🎉

