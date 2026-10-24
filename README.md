# Visary PRSNT

A presentation editor that runs in the browser, built to cover the everyday feature set of PowerPoint, Google Slides, Canva and LibreOffice Impress, with a large library of slide transitions and object animations.

## Run it

```bash
npm install
npm run dev        # http://localhost:5173
npm run build      # production build in dist/
npm test           # unit tests
```

The build is a static site (no server needed), so `dist/` can be hosted anywhere, including GitHub Pages.

## Features

### Slide transitions (59 effects, 163 variants)

Every transition has effect options (direction or variant), duration, easing (smooth, decelerate, accelerate, linear, overshoot, bounce), advance on click, advance automatically after N seconds, and Apply to all slides. Hovering a tile in the Transitions pane plays a live mini preview, and clicking it previews the effect on your real slides.

| Group | Transitions |
| --- | --- |
| Subtle | Fade (smooth, through black, through white), Push, Wipe, Soft wipe, Split, Reveal, Random bars, Shape (circle, diamond, plus, in and out), Uncover, Cover, Flash, Dissolve, Morph |
| Exciting | Fall over, Drape, Curtains, Wind, Prestige, Fracture, Crush, Peel off, Page curl (single or double), Airplane, Origami, Checkerboard, Blinds, Clock (clockwise, counterclockwise, wedge), Wheel (1 to 8 spokes), Ripple, Honeycomb, Glitter (diamonds or hexagons), Vortex, Shred (strips or particles), Switch, Flip, Gallery, Cube, Box, Doors, Comb, Zoom (in, out, zoom and rotate), Strips, TV static, Tile flip |
| Dynamic content | Pan, Ferris wheel, Conveyor, Rotate, Window, Orbit, Fly through (with optional bounce) |
| Creative | Colour wipe, Line wipe, Stack, Chop, Blur, Iris, Rochade, Random |

**Morph** pairs objects that appear on both slides (by object, or by the name you give an object in the Format pane) and animates position, size, rotation and appearance between them. Duplicating a slide keeps object identities, so the usual PowerPoint workflow works. Duplicate, move things, apply Morph.

### Object animations (76 effects)

* **Entrance (24)**: Appear, Fade, Fly in, Float in, Split, Wipe, Shape, Wheel, Random bars, Grow and turn, Zoom, Swivel, Bounce, Drop, Pop, Spin in, Flip, Rise up, Blur, Expand, Stretch, Typewriter, Pan, Elastic
* **Emphasis (20)**: Pulse, Colour pulse, Teeter, Spin, Grow or shrink, Shake, Heartbeat, Wobble, Flicker, Blink, Transparency, Desaturate, Darken, Lighten, Bold flash, Jello, Wave, Swing, Rubber band, Glow
* **Exit (24)**: the mirror of every entrance effect
* **Motion paths (8)**: Line (5 directions), Arc, Circle, Loop de loop, Zigzag, Sine wave, Spiral, Bounce

Each animation has a trigger (on click, with previous, after previous), duration, delay, repeat count, easing and direction. The Animation pane lists the order with step numbers, lets you reorder or remove effects, and Preview plays them on the canvas.

### Editing

* Text boxes with rich text (font, size, colour, highlight, bold, italic, underline, strike, alignment, bullets, numbering, line and letter spacing, vertical alignment), spell check, find and replace
* 34 shapes (basic, arrows, stars, callouts, lines with arrowheads), with text inside, solid or gradient fills, outlines, corner radius
* Pictures (upload, paste, drag and drop, or by URL) with crop to fill, rounded corners, borders and Canva style filter presets plus manual brightness, contrast, saturation, greyscale, sepia, hue and blur
* Tables with header and banded rows, charts (column, bar, line, area, pie, doughnut) with editable data, emoji, slide number fields, YouTube, Vimeo or file videos, hyperlinks to web pages or other slides
* Move, resize (rotation aware, Shift keeps proportions), rotate (Shift snaps to 15 degrees), multi select, marquee select, smart guides and snapping, gridlines, nudge with arrow keys
* Group and ungroup, lock, align and distribute, bring forward or send back, flip, drop shadow, opacity, format painter, copy and paste between slides and decks, unlimited undo and redo
* Layouts (title, title and content, section header, two content, comparison, title only, picture with caption, quote, big number, blank), 10 themes, gradient and picture backgrounds, slide sizes (16:9, 4:3, 16:10, square, story, A4)
* Slide panel with drag to reorder, slide sorter view, hidden slides, speaker notes, zoom, light and dark app theme, right click menus and keyboard shortcuts

### Presenting

* Full screen slide show from the beginning or the current slide, keyboard, click and right click navigation, type a number and Enter to jump
* Pen with colours and erase, laser pointer, black or white screen, slide overview grid, loop until Esc, automatic advance timings, clickable links
* Presenter view in a second window with current and next slide, notes with adjustable size, timer with pause and reset, clock, and remote control of the main show

### Files

* Autosaves in the browser (IndexedDB), with a start screen listing your presentations and templates
* Save and open `.visary.json` files
* Export to **PowerPoint (.pptx)** with text, shapes, pictures, tables, native charts, notes, links, hidden slides and native PowerPoint transitions, including Morph
* Export to **PDF** or print, one slide per page

## Known gaps compared with the desktop suites

These are not implemented yet, and are good candidates for future work.

* Importing `.pptx`, `.odp` or Google Slides files (only the app's own JSON format can be opened)
* Real time multi user collaboration and comments (Google Slides and Canva need a backend for this)
* Object animations are not written into the PowerPoint export (transitions are), and gradient backgrounds export as their first colour
* Audio, slide masters you can edit, SmartArt, equations, 3D models, and image cropping by hand

## Project layout

```
src/model      data types, themes, layouts, shape geometry
src/store      document store with undo and redo
src/render     slide and element rendering shared by editor, thumbnails, show and print
src/editor     editor UI (canvas, ribbon, panes, dialogs)
src/present    transition engine, animation engine, slide show, presenter view
src/io         browser storage, JSON files, PowerPoint export
```
