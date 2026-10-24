# Visary PRSNT

A presentation app for Windows, built with WinUI 3 and the Windows App SDK. It looks and works like PowerPoint, and its point of difference is transitions: a library of modern, cinematic transitions rendered on the GPU with Windows Composition.

## Signature transitions

25 transitions in six families. Every one has a duration, a motion feel (Smooth, Fluid, Snappy, Spring, Linear) and an intensity (Subtle, Balanced, Bold), and most have effect options.

| Family | Transitions |
| --- | --- |
| Essentials | Fade, **Silk** (crossfade through a whisper of blur and a slow zoom), **Drift** (cinematic push in) |
| Motion | **Glide** (parallax, the old slide sinks back), **Velocity** (push with motion blur), **Stack** (lands like a card), **Ribbon** (two halves meet), **Panels** (the old slide lifts away in panels), **Float** |
| Depth | **Depth** (the old slide recedes into the dark), **Portal** (fly through), **Orbit** (3D swing), **Focus pull** (camera rack focus), **Frost** (frosted glass) |
| Light | **Light sweep** (a band of light rides a feathered edge), **Aurora** (blooms open, haloed in the theme colour), **Bloom**, **Spotlight**, **Cinematic** (film letterbox) |
| Reveal | **Feather** (wide soft wipe), **Shutter**, **Mosaic** (tiles surface in a wave) |
| Smart | **Cascade** (objects leave and arrive one by one in reading order), **Morph** (objects on both slides glide, resize and turn into place) |

How they stay elegant:

* The old slide is never faded out underneath the new one, so there is no dip to black mid-transition.
* Every transition is tested to start exactly on the old slide and land exactly on the new one, with no leftover blur, offset, mask or overlay, for every variant, motion and intensity.
* Blur, saturation and exposure use Win2D effects, reveals use feathered gradient masks, depth uses a real perspective transform, and everything runs on the compositor thread so it stays smooth.

## Editor

* PowerPoint style ribbon (File, Home, Insert, Design, Transitions, Animations, Slide Show, View), slide thumbnails with drag to reorder, notes, status bar with zoom
* App colours similar to PowerPoint by default, with 9 Office themes to choose from (PowerPoint colourful, PowerPoint white, Dark gray, Black, Midnight, Ocean, Forest, Lavender, Graphite and gold)
* Text boxes, 25 shapes, lines and arrows, pictures (insert or drag and drop), with fills, gradients, outlines, corner radius, fonts, bullets and numbering
* Select, multi select, marquee, move with smart guides, resize, rotate, align, distribute, arrange, lock, copy, paste, duplicate, undo and redo, find and replace
* 10 slide design themes using fonts that ship with Windows, 8 layouts, gradient and picture backgrounds, 5 slide sizes
* 18 object animations (entrance, emphasis, exit) with On click, With previous and After previous timing

## Slide show

* Full screen show, presenter view on a second screen (current and next slide, notes, timer), keyboard and mouse navigation, black and white screen, laser pointer, loop, automatic timings

## Files

* Saves `.vsry` files (a zip with the presentation and its pictures), with recent files and auto recovery
* Exports to PowerPoint `.pptx`. Each signature transition becomes its closest PowerPoint transition, and Morph stays Morph because objects keep matching names

## Build and run

Requirements: Windows 10 1809 or later and Visual Studio 2022 (17.12 or later) or Visual Studio 2026 with the ".NET desktop development" and "WinUI application development" workloads, plus the .NET 10 SDK.

1. Open `VisaryPRSNT.slnx`.
2. Set `Visary.App` as the startup project.
3. Pick the `x64` platform (or `ARM64` on an ARM PC) and press F5.

The `Visary PRSNT (self test)` launch profile runs the built in UI self test and writes `report.txt` to `%TEMP%\visary-selftest`.

To make a self contained build you can copy to any PC:

```powershell
dotnet publish src/Visary.App -c Release -r win-x64 -p:Platform=x64 -o publish
```

## Tests

```bash
dotnet test tests/Visary.Core.Tests
```

The core library (model, file format, editing commands, transition planner, PowerPoint export) is plain .NET and is tested on any OS, including schema validation of exported `.pptx` files. On Windows, `VisaryPRSNT.exe --selftest <folder>` drives the real app: it plays every transition variant through the GPU player, exercises every pane and ribbon tab, runs the slide show, saves, reopens and exports, then writes `report.txt`.

## Project layout

```
src/Visary.Core        model, themes, editing commands, transition planner, file format, PowerPoint export
src/Visary.App         WinUI 3 app: ribbon, panes, canvas, Composition transition player, slide show
tests/Visary.Core.Tests
```

## Not done yet

* Opening `.pptx` files, rich text with mixed formatting inside one text box, tables, charts, video and audio
* Object animations are not written to `.pptx` (transitions are)
