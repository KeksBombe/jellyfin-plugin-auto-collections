# Auto Collections — Usage Guide

Everything the plugin can match on, how to write the rules, and worked examples.
For what the plugin is and how to install it, see the [README](README.md).

## Contents

- [Matching criteria](#-key-features)
- [Writing collections](#-usage-guide)
- [Expression examples](#expression-examples)
- [Configuration](#-configuration)
- [Collection artwork](#-collection-artwork)
- [Troubleshooting](#-troubleshooting)

## ✨ Key Features

### 🎬 Collection Types

#### Simple Collections
- **Quick Setup**: Easy-to-use interface for basic collections
- **Single Criterion**: Each collection uses one matching criterion
- **Media Filtering**: Filter by Movies, TV Shows, or All content
- **Case Control**: Configurable case-sensitive matching

#### Advanced Collections
- **Boolean Logic**: Combine multiple criteria with AND, OR, NOT operators
- **Complex Expressions**: Use parentheses for grouping and nested logic
- **Multiple Criteria**: Combine different types of filters in one collection

### 🔍 Matching Criteria

#### Content Metadata
- **Title**: Match by words or phrases in titles
- **Filename**: Match by words in the filename
- **Genre**: Group content by genre
- **Studio**: Collect content from specific studios
- **Actor**: Find all content featuring specific actors
- **Director**: Group content by director
- **Writer**: Group content by writer
- **Producer**: Group content by producer
- **Tag**: Match items with specific tags - uses exact matching
- **Overview / Tagline**: Match words in the description or tagline
- **Production Location**: Filter by country/region of origin
- **Library**: Restrict a collection to one library (media folder)

#### Media Type Filtering
- **Movie**: Include only movies
- **Show**: Include only TV series
- **All**: Include both movies and TV shows

#### Rating-Based Filtering
- **Parental Rating**: Filter by age ratings (G, PG, PG-13, R, etc.) - uses exact matching
- **Community Rating**: Match by user community ratings (0-10 scale)
- **Critics Rating**: Filter by professional critic scores
- **Custom Rating**: Use Jellyfin's custom rating field (supports numeric comparisons)

#### Technical Criteria
- **Audio Language**: Match content by audio track language
- **Subtitle Language**: Filter by available subtitle languages
- **Resolution**: Match by video resolution (4K, 1080p, 720p, SD)
- **Dynamic Range**: Match by HDR, HDR10, HLG, Dolby Vision or SDR
- **Audio Channels**: Match by channel count (stereo, 5.1, 7.1)
- **Audio Codec**: Match by codec or track description (DTS, TrueHD, Atmos)
- **Runtime**: Match by duration in minutes
- **Year**: Match by production/release year
- **Release Date**: Filter by release date with day-based comparisons
- **Added Date**: Match by date added to library
- **Episode Air Date**: For TV shows, match by most recent episode air date

#### Play State Criteria
- **Unplayed**: Match items not watched by any user
- **Unwatched**: Alias for Unplayed
- **Watched**: Match items played by at least one user

### ⚙️ Advanced Features

#### Expression Syntax
Advanced collections support complex boolean expressions:
```
(STUDIO "Marvel" AND GENRE "Action") OR (DIRECTOR "Christopher Nolan" AND COMMUNITYRATING ">8.0")
```

#### Supported Keywords
Here are the REAL keywords you can use in expressions:

**Content Criteria:**
- `TITLE` - Match by title
- `FILENAME` - Match by filename
- `GENRE` - Match by genre
- `STUDIO` - Match by studio
- `ACTOR` - Match by actor
- `DIRECTOR` - Match by director
- `WRITER` - Match by writer
- `PRODUCER` - Match by producer
- `TAG` - Match by tag (exact match)
- `OVERVIEW` / `DESCRIPTION` / `PLOT` - Match words in the description
- `TAGLINE` - Match words in the tagline

**Media Type Criteria:**
- `MOVIE` - Match only movies
- `SHOW` - Match only TV shows

**Rating Criteria:**
- `PARENTALRATING` / `PARENTAL` / `RATING` - Match by parental rating
- `COMMUNITYRATING` / `USERRATING` - Match by community rating
- `CRITICSRATING` / `CRITICS` - Match by critics rating
- `CUSTOMRATING` / `CUSTOM` - Match by custom rating

**Location & Language Criteria:**
- `PRODUCTIONLOCATION` / `LOCATION` / `COUNTRY` - Match by production location
- `LANG` - Match by audio language
- `SUB` - Match by subtitle language
- `LIBRARY` - Match by library (media folder) name, exact match

**Quality Criteria:**

These read the item's media streams. TV series carry no streams on the series itself, so
like `LANG` and `SUB` these match movies; use them with `MOVIE` to be explicit.

- `RESOLUTION` / `QUALITY` - `4K`, `1440p`, `1080p`, `720p`, `SD`
- `HDR` / `VIDEORANGE` / `DYNAMICRANGE` - `SDR`, `HDR`, `HDR10`, `HLG`, `DOVI`
- `AUDIOCHANNELS` / `CHANNELS` - channel count, or `stereo` / `5.1` / `7.1`
- `AUDIOCODEC` / `ACODEC` - codec or track description, e.g. `dts`, `truehd`, `atmos`
- `RUNTIME` / `DURATION` / `LENGTH` - runtime in minutes (supports comparisons)

**Temporal Criteria:**
- `YEAR` - Match by production year
- `RELEASEDATE` / `RELEASE` - Match by release date (day-based)
- `ADDEDDATE` / `ADDED` - Match by date added to library (day-based)
- `EPISODEAIRDATE` / `EPISODEAIR` / `LASTAIR` - Match by episode air date (TV shows)

**Play State Criteria:**
- `UNPLAYED` / `UNWATCHED` - Match unplayed items
- `WATCHED` - Match watched items

**Logical Operators:**
- `AND` - Both conditions must be true
- `OR` - Either condition can be true
- `NOT` - Negate a condition

**Grouping:**
- `(` and `)` - Parentheses for expression grouping

#### Numeric Comparisons
Support for comparison operators in numeric fields:
- `COMMUNITYRATING ">8.5"` - Greater than 8.5
- `YEAR ">=2000"` - From year 2000 onwards
- `CRITICSRATING "<=75"` - Critics rating 75 or below
- `CUSTOMRATING "=7"` - Exactly 7

#### Date-Based Filtering
Day-based comparisons for temporal criteria. The value is a **number of days ago**, so
`<=` means recent and `>` means older:

- `ADDEDDATE "<=7"` - Added to the library within the last 7 days
- `ADDEDDATE "<=60"` - Added within roughly the last two months
- `RELEASEDATE "<=1825"` - Released within roughly the last five years
- `RELEASEDATE ">30"` - Released **more than** 30 days ago
- `EPISODEAIRDATE "<=14"` - Newest episode aired within the last 14 days

Because these are relative to the day the sync runs, such collections keep themselves
current: items that stop matching are removed on the next run, so a "recently added"
collection never needs editing by hand.

### 🤖 Automation Features

#### Scheduled Updates
- **Automatic Sync**: Collections update every 24 hours via scheduled task
- **Manual Trigger**: Update collections on-demand from the web interface
- **Real-time Maintenance**: Collections stay current as library changes

#### Collection Management
- **Sorting**: Choose the order of each collection - release year, date added, name, rating, runtime or random, ascending or descending
- **Preview**: See exactly what a sync would add and remove before running it
- **Deduplication**: Prevents duplicate entries in collections
- **Image Assignment**: Automatically sets collection artwork from content, and never replaces artwork you set yourself
- **Smart Naming**: Intelligent default collection names based on criteria
- **Name Protection**: Collections are locked so metadata providers cannot rename them or swap their artwork
- **Orphan Cleanup** (optional, off by default): Delete collections once they are removed from the configuration. Only collections created by this plugin are ever removed

### Sorting

Each collection has its own sort field and direction, set next to it in the plugin page.

| Sort by | Notes |
| --- | --- |
| Release year | Default. Production year, then premiere date |
| Date added | When the item was added to the library |
| Name | Sort name |
| Community rating | |
| Runtime | |
| Random | Reshuffled by Jellyfin each time the collection is opened |

Ascending orders are handed to Jellyfin, which sorts the collection when it is viewed and keeps
it correct as items change. Jellyfin only ever sorts a collection ascending, so descending orders
are produced by controlling the order items are stored in instead. Both work; the ascending ones
are simply cheaper and stay right without a sync.

### Preview (dry run)

Every collection has a **Preview** button, and there is a **Preview All** button above Sync.
Nothing is written to the library by either.

A preview reports the difference against what the collection holds today, not just a list of
matches - a sync also removes items that stopped matching, and that is usually the part worth
checking. It reads the values currently on the page, so a rule can be checked before it is saved.

It flags the cases that normally mean a rule is not doing what was intended:

- nothing matches, or everything currently in the collection would be removed
- more than half the library matches
- `AND` and `OR` are mixed without brackets, where `AND` binds first

### 📊 Configuration Management

#### Import/Export
- **JSON Export**: Backup your collection configurations
- **JSON Import**: Restore configurations or share with others
- **Merge Support**: Add new collections without overwriting existing ones
- **Validation**: Automatic expression validation during import

#### API Integration
- **REST API**: Programmatic access to collection management
- **Automation Ready**: Integrate with external scripts and tools
- **Configuration Endpoints**: Full API for configuration management

### 🔄 Migration & Compatibility

#### Backward Compatibility
- **Legacy Support**: Migrates from old tag-based system
- **Configuration Preservation**: Maintains existing setups during updates
- **Version Safety**: Safe upgrades without data loss

#### Expression Validation
- **Syntax Checking**: Real-time validation of boolean expressions
- **Error Reporting**: Detailed error messages for invalid expressions
- **Auto-Correction**: Fixes common typos in expressions

## 📖 Usage Guide

### Simple Collections Setup

1. Navigate to `Dashboard -> Plugins -> My Plugins -> Auto Collections`
2. Choose match type: Title, Genre, Studio, Actor, Director, Writer, or Tag
3. Set media type filter: All, Movies only, or Shows only
4. Enter search string
5. Configure case sensitivity, and tick **Exact** to require the whole value to match rather than any part of it
6. Choose how the collection is ordered (see [Sorting](#sorting))
7. Set custom collection name (optional)
8. Click **Preview** to see what would happen, then "Save" and "Sync Auto Collections"

### Advanced Collections Setup

1. Scroll to "Advanced Collections" section
2. Enter collection name
3. Build boolean expression using the following REAL keywords:

     **Content Metadata Filters:**
     - `TITLE "text"` - Match items with "text" in the title
     - `FILENAME "text"` - Match items with "text" in the filename
     - `GENRE "name"` - Match items with "name" genre
     - `STUDIO "name"` - Match items from "name" studio
     - `ACTOR "name"` - Match items with "name" actor
     - `DIRECTOR "name"` - Match items with "name" director
     - `WRITER "name"` - Match items with "name" writer
     - `PRODUCER "name"` - Match items with "name" producer
     - `TAG "tag"` - Match items carrying exactly that tag (e.g., `TAG "Best Film"` does not match "Best Film Editing")
     - `OVERVIEW "text"` / `DESCRIPTION "text"` / `PLOT "text"` - Match items whose description contains "text"
     - `TAGLINE "text"` - Match items whose tagline contains "text"
     - `PRODUCTIONLOCATION "location"` / `LOCATION "location"` / `COUNTRY "location"` - Match items by production country/location
     - `LIBRARY "name"` - Only include items from the library (media folder) called "name". Matches the whole name

     **Rating Filters:**
     - `PARENTALRATING "rating"` / `PARENTAL "rating"` / `RATING "rating"` - Match items with specific parental rating (exact match, e.g., "PG" matches only "PG", not "PG-13")
     - `COMMUNITYRATING "value"` / `USERRATING "value"` - Match items by community rating (supports comparison operators)
     - `CRITICSRATING "value"` / `CRITICS "value"` - Match items by critics rating (supports comparison operators)
     - `CUSTOMRATING "value"` / `CUSTOM "value"` - Match by custom rating (string match or numeric comparisons if numeric)

     **Language & Media Filters:**
     - `LANG "language"` - Match items by audio language
     - `SUB "language"` - Match items by subtitle language

     **Temporal Filters:**
     - `YEAR "value"` - Match items by production/release year
     - `RELEASEDATE "value"` / `RELEASE "value"` - Match items by release date with day-based comparisons
     - `ADDEDDATE "value"` / `ADDED "value"` - Match items by date added to library with day-based comparisons
     - `EPISODEAIRDATE "value"` / `EPISODEAIR "value"` / `LASTAIR "value"` - Match by most recent episode air date (for TV shows)

     **Play State Filters:**
     - `UNPLAYED` / `UNWATCHED` - Match items that have not been played by any user
     - `WATCHED` - Match items that have been played by at least one user
     
     **Logic Operators:**
     - `AND` - Both conditions must be true
     - `OR` - Either condition can be true
     - `NOT` - Negate a condition
     - Use parentheses `()` for grouping expressions

   - **Case Sensitive**: Choose whether the matches should be case-sensitive (optional)
4. Click "Save" and then "Sync Auto Collections"

### Expression Examples

#### Basic Combinations
- `GENRE "Action" AND COMMUNITYRATING ">7.0"` - High-rated action content
- `STUDIO "Pixar" OR DIRECTOR "Hayao Miyazaki"` - Animated family content

#### Complex Filtering
- `(GENRE "Comedy" AND YEAR ">=2020") OR (GENRE "Drama" AND CRITICSRATING ">80")` - Recent comedies or critically acclaimed dramas
- `MOVIE AND COMMUNITYRATING ">8.0" AND NOT GENRE "Documentary"` - Highly rated movies excluding documentaries
- `FILENAME "REMUX" OR (FILENAME "2160p" AND (FILENAME "DV" OR FILENAME "HDR"))` – Only 4k HDR content, based on the filename.

#### Geographic and Language Filtering
- `PRODUCTIONLOCATION "Japan" AND (GENRE "Animation" OR LANG "Japanese")` - Japanese animated content
- `LANG "French" AND NOT SUB "English"` - French content without English subtitles

#### Play State Collections
- `MOVIE AND UNPLAYED AND COMMUNITYRATING ">7.5"` - Unwatched high-rated movies
- `SHOW AND WATCHED AND GENRE "Drama"` - Watched drama series

#### Library Filtering
- `TAG "Family" AND LIBRARY "Movies"` - Only from the "Movies" library, ignoring identical titles held in others
- `LIBRARY "4K Movies" AND RESOLUTION "4K"` - Avoids pulling in the 1080p duplicate from another library

#### Quality and Runtime
- `RESOLUTION "4K" AND HDR "DOVI"` - Dolby Vision 4K content
- `NOT RESOLUTION "4K"` - Everything a device that cannot handle 4K can play
- `MOVIE AND RUNTIME "<45"` - Shorts
- `MOVIE AND RUNTIME "<=100" AND GENRE "Comedy"` - Comedies you can finish in an evening
- `CHANNELS ">=6" AND AUDIOCODEC "atmos"` - Atmos content with surround audio

#### Crew and Text
- `WRITER "George Carlin" AND TAG "Stand-Up"` - Specials written by a comedian, without their acting credits
- `OVERVIEW "heist" OR TAGLINE "one last job"` - Matching words in the description rather than the tags

## 🔧 Configuration

### Default Collections
First-time users receive example collections:
- Marvel Universe (Title match)
- Star Wars Collection (Title match)
- Harry Potter Series (Title match)
- Marvel Action (Advanced expression)
- Spielberg or Nolan (Advanced expression)
- Tom Hanks Dramas (Advanced expression)

### Scheduled Tasks
- **Frequency**: Every 24 hours
- **Manual Execution**: Available in `Dashboard -> Scheduled Tasks`
- **Progress Tracking**: Real-time progress during execution

### Endpoints

#### Collection Management
- `POST /AutoCollections/AutoCollections` - Trigger collection sync
- `POST /AutoCollections/Preview` - Preview one collection without changing anything
- `POST /AutoCollections/PreviewAll` - Preview every collection in the posted configuration
- `GET /AutoCollections/ExportConfiguration` - Export configuration as JSON
- `POST /AutoCollections/ImportConfiguration` - Import configuration (overwrite)
- `POST /AutoCollections/AddConfiguration` - Add configuration (merge)

### Configuration Format
```json
{
  "TitleMatchPairs": [
    {
      "TitleMatch": "Marvel",
      "CollectionName": "Marvel Universe",
      "CaseSensitive": false,
      "MatchType": 0,
      "MediaType": 0
    }
  ],
  "ExpressionCollections": [
    {
      "CollectionName": "High Rated Action",
      "Expression": "GENRE \"Action\" AND COMMUNITYRATING \">8.0\"",
      "CaseSensitive": false
    }
  ]
}
```

## 🎨 Collection Artwork

Collections automatically receive artwork from:
- **Person Images**: For actor/director-based collections
- **Content Posters**: From items within the collection
- **Smart Selection**: Prioritizes high-quality images

## 🐛 Troubleshooting

### Common Issues

#### Expression Errors
- Check syntax: Ensure proper quotes around values
- Validate operators: Use correct AND/OR/NOT spelling
- Verify parentheses: Ensure balanced grouping

#### Collection Not Updating
- Run manual sync from plugin settings
- Check scheduled task execution
- Verify library scan completion

#### Import/Export Problems
- Ensure valid JSON format
- Check for special characters in expressions
- Validate collection names are unique

### Debug Features
- **Parse Errors**: Detailed error reporting for invalid expressions
- **Logging**: Comprehensive logging for troubleshooting
- **Validation**: Real-time expression validation
