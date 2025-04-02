# 🏆 Wombastisches Elo Plugin 🏆  
**Counter-StrikeSharp plugin to retrieve Faceit Elo directly in-game!**  

## 🚀 Features  
✅ Retrieve a player's Faceit Elo using their SteamID  
✅ Direct integration with Counter-Strike 2 via Counter-StrikeSharp  
✅ Optimized for fast API requests  
✅ Debug mode for detailed error analysis  

---

## 📥 Installation  
### 1️⃣ **Download the latest release**  
Download the latest `.zip` file from the [Releases](https://github.com/wombastisch/WombastischesEloPlugin/releases) page.  

### 2️⃣ **Extract the files**  
Extract the ZIP file and copy its contents into the **`csgo/counterstrikesharp/plugins/`** folder of your CS2 server.  

### 3️⃣ **Initial Configuration**  
Run `"css_plugins list"` in the server console to check if all CSS plugins are loaded.  
If the plugin is not loaded, use `"css_plugins load WombastischesEloPlugin"` to manually load it.  
If it still does not appear in the list, try restarting the server and check again.  

---

## ⚙️ Debugging / Configuration  
The debug mode logs messages in the server console with the prefix: `[WombastischesEloPlugin]`.
It's still enabled on default. 

The configuration is done editing the WombastischesEloPlugin.json
```bash
{
  "DebugMode": true,  //false to disable server console output
  "FaceitApiKey": "faceit-api-key-here" //enter your faceit api key - you can generate one on https://developers.faceit.com/ create a new app and a new server side API key
}
```

---

## 🎮 Usage  
Use the following command in the in-game chat or console to retrieve the Faceit Elo of all connected players:  

```bash
!faceit
```

---

## ❓ Troubleshooting & Support  
If you encounter issues, check the **server logs** for debug messages.  
Before reporting a new issue, please check if someone else has already encountered the same problem in the **[GitHub Issues](https://github.com/wombastisch/WombastischesEloPlugin/issues)** section.  

---

## 🏗️ Development  
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
