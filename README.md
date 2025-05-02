<h1 align="center">Jellyfin AutoCollections Plugin</h1>

<p align="center">
An enhanced version of Jellyfin Smart Collections Plugin that creates Auto Collections based on Title, Studio, or Genre matching
</p>

## About This Fork

This is an enhanced version of the [original Smart Collections Plugin](https://github.com/johnpc/jellyfin-plugin-smart-collections) by johnpc. The original plugin was designed to create collections based on Tags in your Jellyfin library.

### What the Original Plugin Did

The original plugin by johnpc allowed users to:
- Create collections based on Tags applied to movies and TV series
- Automatically update collections when items were tagged or untagged
- Configure custom collection names for each tag

### What This Enhanced Version Does

This fork extends the original functionality with:
- **Multiple Matching Methods**: Match content by Title, Studio, or Genre (not Tags if you want tags use the original)
- **Flexible Matching**: More options to create diverse and useful collections
- **Extensible Code**: Structured for easy addition of future matching types

## Examples of What You Can Do

With this enhanced version, you can:

1. **Title-based Collections**: Create collections of movies or TV shows containing a specific word or phrase in the title
   - Example: Match "Marvel" in titles to create a Marvel collection

2. **Studio-based Collections**: Group content from the same studio
   - Example: Match "Paramount" to collect all Paramount Pictures productions

3. **Genre-based Collections**: Organize content by genre
   - Example: Match "Thriller" to create a dedicated Thriller collection

The Auto Collections are kept up to date each time the task runs, automatically adding or removing items as they match or no longer match your criteria.

## Install Process

1. In Jellyfin, go to `Dashboard -> Plugins -> Catalog -> Gear Icon (upper left)` add and a repository.
2. Set the Repository name to @KeksBombe (Auto Collections)
3. Set the Repository URL to https://raw.githubusercontent.com/KeksBombe/jellyfin-plugin-auto-collections/refs/heads/main/manifest.json
4. Click "Save"
5. Go to Catalog and search for Auto Collections
6. Click on it and install
7. Restart Jellyfin

## User Guide

1. To set it up, visit `Dashboard -> Plugins -> My Plugins -> Auto Collections`
2. For each auto collection you want to create:
   - Select the match type (Title, Studio, or Genre) from the dropdown
   - Enter the string to match
   - Provide a custom collection name (optional)
   - Choose whether the match should be case-sensitive (optional)
3. Click "Save"
4. Click "Sync Auto Collections" to update your collections immediately
5. Your Collections now exist!

Note: The Auto Collections Sync task is also available in your Scheduled Tasks section and runs automatically every 24 hours.

## Credits

- Original plugin by [johnpc](https://github.com/johnpc/jellyfin-plugin-smart-collections)
- This enhanced fork maintained by [KeksBombe](https://github.com/KeksBombe/jellyfin-plugin-auto-collection)

## License

Same license as the original plugin - see the [LICENSE](LICENSE) file for details.
