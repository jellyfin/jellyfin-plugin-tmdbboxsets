<h1 align="center">Jellyfin TMDb Box Sets Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.media">Jellyfin Project</a></h3>

<p align="center">
<img alt="Logo Banner" src="https://raw.githubusercontent.com/jellyfin/jellyfin-ux/master/branding/SVG/banner-logo-solid.svg?sanitize=true"/>
<br/>
<br/>
<a href="https://github.com/jellyfin/jellyfin-plugin-tmdbboxsets/actions?query=workflow%3A%22Test+Build+Plugin%22">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/workflow/status/jellyfin/jellyfin-plugin-tmdbboxsets/Test%20Build%20Plugin.svg">
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-tmdbboxsets">
<img alt="MIT License" src="https://img.shields.io/github/license/jellyfin/jellyfin-plugin-tmdbboxsets.svg"/>
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-tmdbboxsets/releases">
<img alt="Current Release" src="https://img.shields.io/github/release/jellyfin/jellyfin-plugin-tmdbboxsets.svg"/>
</a>
</p>

## About
Jellyfin TMDb Box Sets plugin is a plugin built with .NET that automatically creates Box Sets and Collections based on TMDb's collection IDs.

## Build Process

1. Clone or download this repository

2. Ensure you have .NET Core SDK setup and installed

3. Build plugin with following command

```sh
dotnet publish --configuration Release --output bin
```

4. Place the resulting file in the `plugins` folder
