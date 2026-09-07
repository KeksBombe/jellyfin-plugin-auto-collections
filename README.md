# Jellyfin Auto Collections Plugin

A powerful Jellyfin plugin that automatically creates and maintains dynamic collections based on flexible criteria. This enhanced fork extends the original Smart Collections plugin with advanced boolean expression support and comprehensive filtering options.

## 🎯 Overview

The Auto Collections plugin enables you to create smart collections that automatically update as your library changes. Collections can be based on simple criteria or complex boolean expressions, allowing for highly specific and dynamic organization of your media library.

Collections come in two flavours:

- **Simple collections** — pick a criterion (title, genre, studio, actor, director, writer or tag), a media type and a search string.
- **Advanced collections** — write a boolean expression combining any of the criteria with `AND`, `OR`, `NOT` and brackets, for example `STUDIO "Marvel Studios" OR TAG "mcu"`.

Either kind re-evaluates on every sync, so collections keep themselves current as the library changes. A **Preview** button shows exactly what a rule would add and remove before anything is written.

📖 **[Usage guide, full criteria reference and examples →](USAGE.md)**

## 🚀 Installation

1. In Jellyfin, go to `Dashboard -> Plugins -> Catalog`
2. Add repository: `@KeksBombe (Auto Collections)`
3. Repository URL: `https://raw.githubusercontent.com/KeksBombe/jellyfin-plugin-auto-collections/refs/heads/main/manifest.json`
4. Click "Save"
5. Search for "Auto Collections" and install
6. Restart Jellyfin

## 📋 Requirements

- **Jellyfin**: Version 10.11 or later
- **Permissions**: Plugin requires collection management permissions

## Share your config or find something cool!

Working configurations shared by other users, and a place to contribute your own:
https://github.com/KeksBombe/jellyfin-auto-collections-configs/tree/main

## 🤝 Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📄 License

This project maintains the same license as the original Smart Collections plugin by johnpc.

## 🙏 Credits

- **Original Plugin**: [johnpc/jellyfin-plugin-smart-collections](https://github.com/johnpc/jellyfin-plugin-smart-collections)
- **Enhanced Fork**: [KeksBombe/jellyfin-plugin-auto-collections](https://github.com/KeksBombe/jellyfin-plugin-auto-collections)
- **Community**: Thanks to all contributors and users

---

**Note**: All images in this repository are mock-up examples for demonstration purposes only. No copyrighted material is included or referenced.
