## About This Fork

This is an enhanced version of the [original Smart Collections Plugin](https://github.com/johnpc/jellyfin-plugin-smart-collections) by johnpc. The original plugin was designed to create collections based on Tags in your Jellyfin library.

### What the Original Plugin Did

The original plugin by johnpc allowed users to:
- Create collections based on Tags applied to movies and TV series
- Automatically update collections when items were tagged or untagged
- Configure custom collection names for each tag

### What This Enhanced Version Does

This fork extends the original functionality with:
- **Two Collection Modes**:
  - **Simple Collections**: Quick setup with single-criterion filtering
  - **Advanced Collections**: Complex filtering with boolean logic expressions
- **Multiple Matching Methods**: Match content by Title, Studio, Genre, Actor, Director, Tags, and more
- **Media Type Filtering**: Filter collections to include only movies, only TV shows, or both
- **Advanced Expression Support**: Create complex collections using boolean expressions (AND, OR, NOT)
- **Enhanced Filtering Options**:
  - Filter by content metadata (title, genre, studio, actor, director, tags)
  - Filter by ratings (parental ratings, community ratings, critics ratings)
  - Filter by production locations/countries
  - Filter by audio language and subtitle language
   - Filter by user-defined custom rating (Jellyfin's CustomRating field)

- **Boolean Logic**: Combine multiple criteria with AND, OR, NOT operators and parentheses grouping
- **Import/Export**: Easily backup and restore your collection configurations as JSON
- **Case Sensitivity Control**: Choose whether matches should be case-sensitive or not
- **Scheduled Synchronization**: Collections automatically update on a schedule

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

6. **Tag-based Collections**: Group content with specific tags
   - Example: `TAG "Family-Friendly"` to create a collection of content tagged as family-friendly

7. **Rating-based Collections**: Create collections based on ratings
   - Example: `PARENTALRATING "PG-13"` for PG-13 content
   - Example: `COMMUNITYRATING ">8.5"` for highly-rated content (8.5+ stars)

8. **Critics Rating Collections**: Group by critics ratings
   - Example: `CRITICSRATING ">75"` for critically acclaimed content

8b. **Custom Rating Collections**: Use Jellyfin's CustomRating field (user-supplied)
   - Example: `CUSTOMRATING "PG-13" OR CUSTOMRATING "PG"`
   - Example: `CUSTOMRATING "<=7" AND MOVIE` (when you store numeric suitability values like Common Sense Media ages)

9. **Geographic Collections**: Group by production location
   - Example: `PRODUCTIONLOCATION "France"` for French productions

10. **Audio Language Collections**: Group by audio language
    - Example: `LANG "Japanese"` for content with Japanese audio
    - Example: `LANG "German" AND GENRE "Comedy"` for German comedy films

11. **Subtitle Collections**: Group by available subtitle tracks
    - Example: `SUB "English"` for content with English subtitles
    - Example: `LANG "French" AND NOT SUB "English"` for French audio content without English subtitles

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

The Auto Collections plugin offers two modes for creating collections: **Simple Collections** and **Advanced Collections**.

### Simple Collections

Simple Collections provide an easy-to-use interface for creating basic collections with a single filter criterion.

1. To set it up, visit `Dashboard -> Plugins -> My Plugins -> Auto Collections`
2. For each simple auto collection you want to create:
   - **Match Type**: Select one of the following from the dropdown:
     - **Title**: Match by words or phrases in the title
     - **Studio**: Match by production studio
     - **Genre**: Match by genre
     - **Actor**: Match by actor name
     - **Director**: Match by director name
   - **Media Type**: Filter the collection content by:
     - **All**: Include both movies and TV shows (default)
     - **Movies**: Include only movies
     - **Shows**: Include only TV shows
   - **String to Match**: Enter the text to match against the selected match type
   - **Collection Name**: Provide a custom collection name (optional)
   - **Case Sensitive**: Choose whether the match should be case-sensitive (optional)
3. Click "Save"
4. Click "Sync Auto Collections" to update your collections immediately
5. Your Collections now exist!

**Simple Collection Limitations:**
- Each simple collection uses exactly one match criterion (Title, Studio, etc.)
- Cannot combine multiple criteria (e.g., can't match both a title AND a genre)
- Cannot use complex filtering beyond the basic media type filter

### Advanced Collections

Advanced Collections provide much more powerful filtering capabilities with boolean expressions.

1. In the Auto Collections settings, scroll to the "Advanced Collections" section
2. For each advanced collection:
   - **Collection Name**: Enter a name for your collection
   - **Expression**: Create a boolean expression using the following criteria types:

     **Content Metadata Filters:**
     - `TITLE "text"` - Match items with "text" in the title
     - `GENRE "name"` - Match items with "name" genre
     - `STUDIO "name"` - Match items from "name" studio
     - `ACTOR "name"` - Match items with "name" actor
     - `DIRECTOR "name"` - Match items with "name" director
     - `TAG "tag"` - Match items with "tag" in their tags
     - `PRODUCTIONLOCATION "location"` - Match items by production country/location

     **Rating Filters:**
     - `PARENTALRATING "rating"` - Match items with specific parental rating (e.g., "PG-13", "R")
     - `COMMUNITYRATING "value"` - Match items by community rating (supports comparison operators)
     - `CRITICSRATING "value"` - Match items by critics rating (supports comparison operators)
   - `CUSTOMRATING "value"` - Match by custom rating (string match or numeric comparisons if numeric)

     **Media Type Filters:**
     - `MOVIE` - Match only movies
     - `SHOW` - Match only TV shows
     
     **Logic Operators:**
     - `AND` - Both conditions must be true
     - `OR` - Either condition can be true
     - `NOT` - Negate a condition
     - Use parentheses `()` for grouping expressions

   - **Case Sensitive**: Choose whether the matches should be case-sensitive (optional)
3. Click "Save" and then "Sync Auto Collections"

**Advanced Collection Benefits:**
- Combine multiple criteria (e.g., match a genre AND a specific actor)
- Create complex exclusion rules (e.g., match a genre BUT NOT a certain director)
- Use parentheses to group expressions for complex logic
- Apply multiple filters simultaneously
- Create highly specific, targeted collections

### Numeric Rating Syntax

For community and critics ratings in Advanced Collections, you can use comparison operators:
- `COMMUNITYRATING ">8"` - Greater than 8
- `COMMUNITYRATING ">=9.5"` - Greater than or equal to 9.5
- `COMMUNITYRATING "<6"` - Less than 6
- `COMMUNITYRATING "=7"` - Exactly 7
- `COMMUNITYRATING "7"` - Exactly 7 (equals sign is optional)

For custom rating (if you store numeric values, e.g. child age suitability):
- `CUSTOMRATING "<=7"` - Items whose custom rating numeric value is 7 or below
- Numeric comparison works only if the item's CustomRating field parses to a number; otherwise it falls back to substring matching.

### Numeric Year Filtering

For filtering by production year in Advanced Collections:
- `YEAR ">2000"` - Content released after 2000
- `YEAR ">=1990"` - Content from 1990 and later
- `YEAR "<2000"` - Content released before 2000
- `YEAR "<=1989"` - Content from 1989 and earlier
- `YEAR "=2020"` - Content from exactly 2020
- `YEAR "2020"` - Content from exactly 2020 (equals sign is optional)

For year ranges (like a decade), use multiple YEAR filters connected with AND:
- `YEAR ">=2000" AND YEAR "<=2010"` - Content from the 2000s decade

Note: Using a syntax like `YEAR ">=2000 AND <=2010"` will not work as expected. Always use separate YEAR expressions for ranges.

### Useful Advanced Expression Examples

- **Recent High-Quality Movies**: `MOVIE AND COMMUNITYRATING ">7.5" AND CRITICSRATING ">70"`
- **Family-Friendly Content**: `PARENTALRATING "G" OR PARENTALRATING "PG"`
- **Exclude Specific Content**: `GENRE "Horror" AND NOT DIRECTOR "Jordan Peele"`
- **Content from Specific Countries**: `PRODUCTIONLOCATION "Japan" AND GENRE "Animation"`
- **Actor in Multiple Genres**: `ACTOR "Tom Hanks" AND (GENRE "Drama" OR GENRE "Comedy")`
- **TV Shows with High Ratings**: `SHOW AND COMMUNITYRATING ">8" AND GENRE "Drama"`
- **Japanese Audio Content**: `LANG "Japanese"`
- **German Comedy Films**: `LANG "German" AND GENRE "Comedy"`
- **Content with English Subtitles**: `SUB "English"`
- **French Audio without English Subtitles**: `LANG "French" AND NOT SUB "English"`

### Import/Export Configuration

You can easily backup and share your collection configurations:

1. In the Auto Collections settings page, find the Import/Export section
2. Click "Export Filter Config (JSON)" to download your current configuration
3. Use "Import Filter Config (JSON)" to upload a configuration file
4. Click "Save" and then "Sync Auto Collections"

## Scheduled Synchronization

The Auto Collections Sync task is available in your Jellyfin Scheduled Tasks section and runs automatically every 24 hours. This ensures that your collections stay up-to-date as new content is added to your library or metadata changes.

You can also manually trigger a sync at any time:
1. From the Auto Collections plugin settings page by clicking "Sync Auto Collections"
2. From the Jellyfin Dashboard -> Scheduled Tasks section

## Comparison Between Simple and Advanced Collections

| Feature | Simple Collections | Advanced Collections |
|---------|-------------------|----------------------|
| Interface | User-friendly dropdowns | Text-based expression language |
| Filtering criteria | Single filter per collection | Multiple filters per collection |
| Boolean logic | Not supported | AND, OR, NOT operators with parentheses |
| Media type filtering | Basic | Same, but can be combined with other criteria |
| Rating filters | Not supported | Supports parental, community and critics ratings |
| Comparison operators | Not supported | Supports >, <, >=, <= for numeric values |
| Case sensitivity | Configurable | Configurable |
| Best for | Quick, simple collections | Complex, highly specific collections |

## Credits

- Original plugin by [johnpc](https://github.com/johnpc/jellyfin-plugin-smart-collections)
- This enhanced fork maintained by [KeksBombe](https://github.com/KeksBombe/jellyfin-plugin-auto-collections)

## License

Same license as the original plugin - see the [LICENSE](LICENSE) file for details.
