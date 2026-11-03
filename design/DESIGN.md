# Hackathon Solution — design system

Codified from the live Angular app (`Frontend/src/styles.scss` and interview overview). Use this for demo slides and marketing surfaces.

## Color

| Token | Value | Use |
| --- | --- | --- |
| text | `#111111` | Headings, body |
| muted | `#5C6370` | Labels, secondary copy |
| accent | `#1B64F2` | Score, links, focus, eyebrows |
| accent-hover | `#1554CC` | Interactive hover |
| surface | `#FFFFFF` | Slide / card fill |
| surface-muted | `#F4F5F7` | Recessed panels |
| border | `#E4E7EC` | Hairlines |
| border-strong | `#D0D5DD` | Controls |
| success | `#027A48` | Positive verdict |
| danger | `#B42318` | Failures only |

No gradients. No extra palette.

## Typography

- Family: Montserrat, Segoe UI, system-ui, sans-serif
- Weights: 400 body, 650 labels, 700 titles, 800 display / scores
- Display scores: 800, tight tracking (`-0.04em`)
- Eyebrows: 0.72rem, 700, `0.08em`, uppercase, accent
- Body line-height 1.45–1.55. No markdown in spoken UI; slides may use lists.

## Spacing

Scale: 4 / 8 / 12 / 16 / 24 / 32 / 48. Cards 10px radius, controls 8px. Gap in stacks 12–16px.

## Layout

Content max ~42–48rem in product. Demo slides are 16:9, padded 48–64px, left-aligned. Avoid centered hero blocks.

## Components

- Eyebrow + title + muted lead
- Score: large numeral + `/ 10 overall`
- Fact chips: 1px border, 8px radius, uppercase dt
- Lists with 4px gap between items
- Focus: 2px accent outline, 2px offset

## Motion

Optional 160ms ease-out fade. Honor `prefers-reduced-motion`. Keyboard-first for decks (arrows).

## Voice

Warm, professional, concise. Scores 1–10. Camera copy never states cheating as a fact.

## Brand assets

Product wordmark: “Hackathon Solution” in 800 weight. Accent blue on white.

## Anti-patterns

- Gradient backgrounds, glow, glassmorphism
- Drop shadows heavier than `0 14px 40px rgb(17 17 17 / 14%)`
- Mixing a second accent color
- Centered walls of body text
- Fake screenshots or lorem
