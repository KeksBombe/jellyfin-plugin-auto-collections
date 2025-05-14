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
- **Multiple Matching Methods**: Match content by Title, Studio, Genre, Actor, or Director
- **Media Type Filtering**: Filter collections to include only movies, only TV shows, or both
- **Advanced Expression Support**: Create complex collections using boolean expressions (AND, OR, NOT)
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
   
4. **Media Type Filtering**: Separate movies from TV shows
   - Example: Match "Action" genre but only include movies, not TV shows

5. **Advanced Collections**: Create collections using boolean expressions
   - Example: `STUDIO "Marvel" AND (GENRE "Action" OR ACTOR "Robert Downey Jr.")`

The Auto Collections are kept up to date each time the task runs, automatically adding or removing items as they match or no longer match your criteria.

Settings I use for 2x Collection by Title, 1x Studio, 1x Genre
![image](https://github.com/user-attachments/assets/8c44b541-3381-44df-9742-4c7b2d486403)

Grouped by Genre:
![Screenshot 2025-05-02 173135](https://github.com/user-attachments/assets/e9a66659-7df2-4f45-aec7-d199b8b94d03)

Grouped by Title:
![Screenshot 2025-05-02 174300](https://github.com/user-attachments/assets/8bf7e874-d8a9-4778-a3dd-8764cc2b7532)

Grouped by Studio:
![Screenshot 2025-05-02 173910](https://github.com/user-attachments/assets/b3d8847b-5393-487f-8933-2d556d8ac2cc)

Disclamer:
All images used in this repository are purely **mock-up** or **placeholder** examples and are **not** representations of real media. They are intended solely to demonstrate the functionality of the plugin.
No copyrighted material is included or referenced in this project. Any resemblance to actual movie posters or media is purely coincidental.
This project does not use or distribute any copyrighted media content.

## Install Process

1. In Jellyfin, go to `Dashboard -> Plugins -> Catalog -> Gear Icon (upper left)` add and a repository.
2. Set the Repository name to @KeksBombe (Auto Collections)
3. Set the Repository URL to https://raw.githubusercontent.com/KeksBombe/jellyfin-plugin-auto-collections/refs/heads/main/manifest.json
4. Click "Save"
5. Go to Catalog and search for Auto Collections
6. Click on it and install
7. Restart Jellyfin

## User Guide

### Simple Collections

1. To set it up, visit `Dashboard -> Plugins -> My Plugins -> Auto Collections`
2. For each auto collection you want to create:
   - Select the match type (Title, Studio, Genre, Actor, or Director) from the dropdown
   - Select the media type (All, Movies, or Shows) from the dropdown
   - Enter the string to match
   - Provide a custom collection name (optional)
   - Choose whether the match should be case-sensitive (optional)
3. Click "Save"
4. Click "Sync Auto Collections" to update your collections immediately
5. Your Collections now exist!

### Advanced Collections

For more complex collections, you can use the Advanced Collections feature:

1. In the Auto Collections settings, scroll to the "Advanced Collections" section
2. For each advanced collection:
   - Enter a collection name
   - Create an expression using the following syntax:
     - `TITLE "text"` - Match items with "text" in the title
     - `GENRE "name"` - Match items with "name" genre
     - `STUDIO "name"` - Match items from "name" studio
     - `ACTOR "name"` - Match items with "name" actor
     - `DIRECTOR "name"` - Match items with "name" director
     - `MOVIE` - Match only movies
     - `SHOW` - Match only TV shows
     - Combine with `AND`, `OR`, `NOT`, and parentheses
   - Choose whether the matches should be case-sensitive (optional)
3. Click "Save" and then "Sync Auto Collections"

Example expressions:
- `TITLE "Star Wars" AND MOVIE` - Only Star Wars movies
- `GENRE "Comedy" AND SHOW` - Only comedy TV shows
- `STUDIO "Marvel" AND (MOVIE OR ACTOR "Chris Evans")` - Marvel movies or anything with Chris Evans

Note: The Auto Collections Sync task is also available in your Scheduled Tasks section and runs automatically every 24 hours.

## Credits

- Original plugin by [johnpc](https://github.com/johnpc/jellyfin-plugin-smart-collections)
- This enhanced fork maintained by [KeksBombe](https://github.com/KeksBombe/jellyfin-plugin-auto-collection)

## License

Same license as the original plugin - see the [LICENSE](LICENSE) file for details.
