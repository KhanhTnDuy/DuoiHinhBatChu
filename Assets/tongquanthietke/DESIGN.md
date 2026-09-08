---
name: Modern Fluent Guessing Game
colors:
  surface: '#f8f9ff'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#454655'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#767686'
  outline-variant: '#c6c5d7'
  surface-tint: '#3f4cd9'
  primary: '#1c29be'
  on-primary: '#ffffff'
  primary-container: '#3a47d5'
  on-primary-container: '#cbceff'
  inverse-primary: '#bdc2ff'
  secondary: '#00677f'
  on-secondary: '#ffffff'
  secondary-container: '#00d2ff'
  on-secondary-container: '#00566a'
  tertiary: '#5e3c00'
  on-tertiary: '#ffffff'
  tertiary-container: '#7d5100'
  on-tertiary-container: '#ffc97f'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#e0e0ff'
  primary-fixed-dim: '#bdc2ff'
  on-primary-fixed: '#000668'
  on-primary-fixed-variant: '#222fc2'
  secondary-fixed: '#b6ebff'
  secondary-fixed-dim: '#47d6ff'
  on-secondary-fixed: '#001f28'
  on-secondary-fixed-variant: '#004e60'
  tertiary-fixed: '#ffddb4'
  tertiary-fixed-dim: '#ffb952'
  on-tertiary-fixed: '#291800'
  on-tertiary-fixed-variant: '#633f00'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
typography:
  headline-xl:
    fontFamily: plusJakartaSans
    fontSize: 40px
    fontWeight: '800'
    lineHeight: 48px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: plusJakartaSans
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: plusJakartaSans
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: plusJakartaSans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: plusJakartaSans
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: plusJakartaSans
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: plusJakartaSans
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  tile-char:
    fontFamily: plusJakartaSans
    fontSize: 26px
    fontWeight: '800'
    lineHeight: 32px
    letterSpacing: 0.02em
  hud-counter:
    fontFamily: jetbrainsMono
    fontSize: 18px
    fontWeight: '700'
    lineHeight: 22px
    letterSpacing: -0.01em
  label-md:
    fontFamily: jetbrainsMono
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.04em
  label-sm:
    fontFamily: jetbrainsMono
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
    letterSpacing: 0.06em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  space-2xs: 0.25rem
  space-xs: 0.5rem
  space-sm: 0.75rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
  space-2xl: 3rem
  gutter-canvas: 1.5rem
  sidebar-width: 18rem
  tile-gap: 0.625rem
---

## Brand & Style

The design system merges the polished precision of Windows 11 Fluent Design and modern WPF desktop architectures with the vibrant, tactile thrill of a casual guessing game. The aesthetic evokes clarity, playful energy, and premium desktop craftsmanship. It engages players through fluid micro-interactions, clean structural geometry, and subtle depth layers reminiscent of Mica and Acrylic materials.

The visual direction centers on modern desktop minimalism elevated by soft translucent layers, crisp typography, and high-energy interactive touchpoints. Key visual traits:
- **Atmospheric Depth:** Semi-transparent surfaces with gentle backdrop blurs mimic desktop Mica and Acrylic materials, giving views a native Windows feel.
- **Playful Tactility:** Interactive components, such as letter tiles and reveal cards, exhibit physical affordance through dynamic elevation, soft ambient shadows, and snappy spring-like transition states.
- **Energetic Focus:** High-contrast accent colors frame game mechanics without visual clutter, directing focus to image reveals, countdowns, and guess inputs.

## Colors

The palette balances authoritative deep indigo foundations with luminous electric blue energy and vivid amber accents, grounded by slate neutrals and translucent white mica backdrops.

- **Primary (`#3A47D5` - Deep Indigo):** Provides structural anchors, prominent window headers, main navigation tabs, and default interactive actions.
- **Secondary (`#00D2FF` - Electric Blue):** Drives focus states, progressive reveal highlights, active score multipliers, and dynamic glowing borders.
- **Tertiary (`#FFAA00` - Energetic Amber):** Highlights game-critical mechanics: streak counters, coin rewards, hint triggers, countdown timers, and victory states.
- **Neutral (`#64748B` - Slate Neutral):** Establishes subtle borders, secondary labels, disabled element tracks, and inactive tile outlines.
- **Surface Foundations:**
  - Background Base: `#F1F5F9` (Mica-inspired canvas with soft noise or tinting).
  - Layer Surfaces: `rgba(255, 255, 255, 0.78)` to `rgba(255, 255, 255, 0.92)` with `backdrop-filter: blur(20px) saturate(125%)`.
  - Dark Contrast Canvas (Optional window frames / chrome): `#0F172A` with translucent panel overlays.

## Typography

The type system blends friendly, geometric warmth with crisp digital precision:

- **Plus Jakarta Sans:** Drives all structural headlines, titles, dialogs, and letter inputs. Its rounded geometry complements the playful tone of the game while maintaining modern Fluent polish.
- **JetBrains Mono:** Dedicated to the Game HUD, dynamic score tallies, timers, streak multipliers, and telemetry indicators. The fixed-width nature ensures numeric layouts remain stationary during rapid counter increments.
- **Character Sizing & Case:**
  - Letter tiles use `tile-char` in uppercase for instant optical identification.
  - HUD tags and metadata use `label-sm` or `label-md` in uppercase to create an architectural contrast against organic gameplay imagery.

## Elevation & Depth

Visual hierarchy leverages Windows Fluent acrylic transparency combined with directional tinted drop shadows.

- **Layer 0 (Canvas Base):** Solid background (`#F1F5F9`) with faint radial gradient accents using primary/secondary tints.
- **Layer 1 (Card & Content Panels):** Acrylic surface (`rgba(255, 255, 255, 0.85)`, `backdrop-filter: blur(24px)`). Outlined with an inner highlight: `1px solid rgba(255, 255, 255, 0.6)` and ambient drop shadow: `0 4px 16px -2px rgba(15, 23, 42, 0.06), 0 2px 6px -1px rgba(15, 23, 42, 0.04)`.
- **Layer 2 (Floating HUD & Interactive Tiles):** Higher contrast glass panels (`rgba(255, 255, 255, 0.95)`). Outlined with `1px solid rgba(0, 210, 255, 0.25)`. Shadow: `0 10px 25px -4px rgba(58, 71, 213, 0.12), 0 4px 10px -2px rgba(15, 23, 42, 0.05)`.
- **Layer 3 (Modals, Reveal Dialogs & Overlays):** Full acrylic scrim with an elevated dialog surface. Shadow: `0 24px 48px -8px rgba(15, 23, 42, 0.25), 0 0 0 1px rgba(255, 255, 255, 0.5) inset`.
- **Active / Pressed State:** Physical compression effect—cards sink by 2px with an inner shadow (`inset 0 2px 4px rgba(0, 0, 0, 0.12)`).

## Shapes

The design system employs a rounded aesthetic consistent with Windows 11 Fluent specifications:

- **Base Radius (`rounded-md` - 8px):** Input elements, small tooltips, individual HUD badges, and list rows.
- **Card & Tile Radius (`rounded-lg` - 16px):** Primary letter tiles, picture display containers, interactive cards, and tool docks.
- **Surface & Window Radius (`rounded-xl` - 24px):** Major panel sections, modal dialogs, victory celebration banners, and drawer overlays.
- **Pill Radius (`rounded-full`):** Filter tags, action buttons, progress bars, and coin/streak status chips.

## Components

### Buttons
- **Primary Button:** Gradient fill (`linear-gradient(135deg, #3A47D5 0%, #00D2FF 100%)`), text `#FFFFFF`, rounded pill shape. Focus ring uses an electric blue outer border with 2px offset. Hover causes subtle vertical lift (`translateY(-1px)`) and increased shadow intensity.
- **Secondary / Ghost Button:** Transparent background, `1px solid rgba(100, 116, 139, 0.3)`, text `#3A47D5`. On hover, fills with `rgba(58, 71, 213, 0.08)`.
- **Tertiary (Action / Hint Button):** Solid `#FFAA00` fill with white or dark slate typography, providing immediate visual affordance for hints and power-ups.

### Letter Tiles (Game-Specific)
- **Keyboard / Selection Tiles:** 48x48px (scalable to 56x56px on 4K), rounded 12px corners, semi-opaque white background (`rgba(255, 255, 255, 0.9)`), distinct bottom border bevel (`3px solid #CBD5E1`) creating a physical keyboard keycap aesthetic. Active state triggers a slight depress animation (`transform: translateY(2px)` with bottom border reducing to 1px).
- **Answer Slots:** Empty state renders as a dashed border (`2px dashed rgba(100, 116, 139, 0.4)`); filled state adopts a smooth gradient border (`#00D2FF` to `#3A47D5`) with an illuminated background glow.

### Cards & Picture Frame
- **Image Viewport:** Encased in a `rounded-xl` (24px) container with an internal border (`1px solid rgba(255, 255, 255, 0.5)`). Progressive reveal tiles overlay the image with frosted acrylic glass plates that shatter or fade out via smooth opacity-and-scale animations upon hint unlock.
- **Info Cards:** Acrylic surface with `16px` padding, displaying level details, difficulty badges, and category tags.

### HUD Chips & Stat Trackers
- **Streak & Coin Badges:** Pill-shaped translucent bars containing an SVG icon, numeric value in `jetbrainsMono`, and subtle gradient fill. The amber coin chip utilizes `rgba(255, 170, 0, 0.12)` background with `#FFAA00` border and text.

### Input Fields
- Rounded 8px height, `rgba(255, 255, 255, 0.8)` background, border `1px solid rgba(100, 116, 139, 0.25)`.
- On focus: border transitions to `#00D2FF` alongside a `0 0 0 3px rgba(0, 210, 255, 0.2)` glow ring.

### Checkboxes & Radio Buttons
- Rounded 6px for checkboxes, full circle for radios. Primary indigo fill on selection with an animated white check icon. Focus displays an electric blue halo.