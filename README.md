# Jellyfin Auto Collections Plugin

A powerful Jellyfin plugin that automatically creates and maintains dynamic collections based on flexible criteria. This enhanced fork extends the original Smart Collections plugin with advanced boolean expression support and comprehensive filtering options.

## 🎯 Overview

The Auto Collections plugin enables you to create smart collections that automatically update as your library changes. Collections can be based on simple criteria or complex boolean expressions, allowing for highly specific and dynamic organization of your media library.

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
- **Genre**: Group content by genre
- **Studio**: Collect content from specific studios
- **Actor**: Find all content featuring specific actors
- **Director**: Group content by director
- **Tag**: Match items with specific tags
- **Production Location**: Filter by country/region of origin

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
- `GENRE` - Match by genre
- `STUDIO` - Match by studio
- `ACTOR` - Match by actor
- `DIRECTOR` - Match by director
- `TAG` - Match by tag

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
Day-based comparisons for temporal criteria:
- `RELEASEDATE ">30"` - Released within last 30 days
- `ADDEDDATE "<=7"` - Added to library within last 7 days
- `EPISODEAIRDATE ">14"` - TV episodes aired within last 14 days

### 🤖 Automation Features

#### Scheduled Updates
- **Automatic Sync**: Collections update every 24 hours via scheduled task
- **Manual Trigger**: Update collections on-demand from the web interface
- **Real-time Maintenance**: Collections stay current as library changes

#### Collection Management
- **Auto-Sorting**: Items sorted by production year and premiere date
- **Deduplication**: Prevents duplicate entries in collections
- **Image Assignment**: Automatically sets collection artwork from content
- **Smart Naming**: Intelligent default collection names based on criteria

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

## 🚀 Installation

1. In Jellyfin, go to `Dashboard -> Plugins -> Catalog`
2. Add repository: `@KeksBombe (Auto Collections)`
3. Repository URL: `https://raw.githubusercontent.com/KeksBombe/jellyfin-plugin-auto-collections/refs/heads/main/manifest.json`
4. Click "Save"
5. Search for "Auto Collections" and install
6. Restart Jellyfin

## 📖 Usage Guide

### Simple Collections Setup

1. Navigate to `Dashboard -> Plugins -> My Plugins -> Auto Collections`
2. Choose match type: Title, Genre, Studio, Actor, or Director
3. Set media type filter: All, Movies only, or Shows only
4. Enter search string
5. Configure case sensitivity
6. Set custom collection name (optional)
7. Click "Save" and "Sync Auto Collections"

### Advanced Collections Setup

1. Scroll to "Advanced Collections" section
2. Enter collection name
3. Build boolean expression using the following REAL keywords:

     **Content Metadata Filters:**
     - `TITLE "text"` - Match items with "text" in the title
     - `GENRE "name"` - Match items with "name" genre
     - `STUDIO "name"` - Match items from "name" studio
     - `ACTOR "name"` - Match items with "name" actor
     - `DIRECTOR "name"` - Match items with "name" director
     - `TAG "tag"` - Match items with "tag" in their tags
     - `PRODUCTIONLOCATION "location"` / `LOCATION "location"` / `COUNTRY "location"` - Match items by production country/location

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

#### Geographic and Language Filtering
- `PRODUCTIONLOCATION "Japan" AND (GENRE "Animation" OR LANG "Japanese")` - Japanese animated content
- `LANG "French" AND NOT SUB "English"` - French content without English subtitles

#### Play State Collections
- `MOVIE AND UNPLAYED AND COMMUNITYRATING ">7.5"` - Unwatched high-rated movies
- `SHOW AND WATCHED AND GENRE "Drama"` - Watched drama series

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

## 📋 Requirements

- **Jellyfin**: Version 10.9.9.0 or later
- **.NET**: Compatible with Jellyfin's runtime
- **Permissions**: Plugin requires collection management permissions

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