# 🚀 LaraStack

> A free, open-source local development environment for Laravel on Windows — like XAMPP, but built specifically for Laravel developers.

![LaraStack Dashboard](https://raw.githubusercontent.com/Faizan-Sheikh007/larastack/main/screenshot.png)

---

## ✨ Features

- **One-click installer** — Downloads and installs PHP 8.3, MySQL 8.0, and phpMyAdmin automatically
- **Service control** — Start/Stop PHP and MySQL individually or all at once from the dashboard
- **php.ini Editor** — Edit common PHP settings visually with a built-in raw editor and live search
- **System Monitor** — Real-time CPU, RAM, and disk usage charts
- **Log Viewer** — View PHP and server logs in real time
- **File Manager** — Quick access to your web root directory
- **Database Manager** — Launch phpMyAdmin in one click
- **System Tray** — Runs quietly in the background, accessible from the tray
- **Auto-updater** — Receives announcements and updates from the LaraStack admin panel

---

## 📦 Download

👉 **[Download Latest Release](https://github.com/Faizan-Sheikh007/larastack/releases/latest)**

| File | Description |
|------|-------------|
| `LaraStack_Setup_v2.0.0.exe` | Windows installer (recommended) |

**System Requirements:**
- Windows 10 / 11 (64-bit)
- .NET 8 Runtime ([Download here](https://dotnet.microsoft.com/download/dotnet/8.0))
- 500 MB free disk space (more for PHP/MySQL)

---

## 🛠️ Installation

1. Download `LaraStack_Setup_v2.0.0.exe` from the [Releases](https://github.com/Faizan-Sheikh007/larastack/releases) page
2. Run the installer and follow the setup wizard
3. Launch **LaraStack** from the desktop shortcut or Start Menu
4. Go to the **Installer** tab and click **Install All** to download PHP, MySQL, and phpMyAdmin
5. Click **Start All** — you're ready to develop! 🎉

---

## 🖥️ Screenshots

| Dashboard | php.ini Editor |
|-----------|----------------|
| ![Dashboard](https://raw.githubusercontent.com/Faizan-Sheikh007/larastack/main/screenshots/dashboard.png) | ![PHP INI](https://raw.githubusercontent.com/Faizan-Sheikh007/larastack/main/screenshots/phpini.png) |

---

## 🔧 Built With

- [.NET 8](https://dotnet.microsoft.com/) — Application framework
- [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/) — UI framework
- [PHP 8.3](https://www.php.net/) — PHP runtime (downloaded by installer)
- [MySQL 8.0](https://www.mysql.com/) — Database (downloaded by installer)
- [phpMyAdmin 5.2](https://www.phpmyadmin.net/) — DB UI (downloaded by installer)
- [Inno Setup](https://jrsoftware.org/isinfo.php) — Windows installer compiler

---

## 📁 Project Structure

```
LaraStack/
├── Views/              # WPF UI pages (Dashboard, Services, Installer, etc.)
├── Services/           # Backend logic (ServerService, InstallerService, etc.)
├── Models/             # Data models
├── App.xaml            # Application entry point & global styles
├── MainWindow.xaml     # Main window with sidebar navigation
└── LaraStack.User.csproj
```

---

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

1. Fork the repository
2. Create your feature branch: `git checkout -b feature/my-feature`
3. Commit your changes: `git commit -m 'Add my feature'`
4. Push to the branch: `git push origin feature/my-feature`
5. Open a Pull Request

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 🌐 Website

[larastack.click](https://larastack.click) — Official download page *(coming soon)*

---

## 👤 Author

**Faizan Sheikh**
- GitHub: [@Faizan-Sheikh007](https://github.com/Faizan-Sheikh007)

---

> ⭐ If LaraStack saved you time, please give it a star on GitHub!
