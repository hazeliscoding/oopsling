# Oopsling brand

The logo is option 1, "Bandage", picked on 2026-10-25 from four takes on the claude.ai design canvas "Oopsling logo": a round coral critter with a crossed bandage on its head, eyes shut in a sheepish smile, and a sweat drop. It says "oops" and owns it.

- `mark.svg`: the critter alone, transparent background. The black outline disappears on dark backgrounds, but the coral fill carries the shape, so it has no dark variant.
- `lockup.svg` / `lockup-dark.svg`: the mark plus the "oopsling" wordmark, for light and dark backgrounds, as in the README's title.
- The wordmark is Fredoka SemiBold (SIL Open Font License), lowercase, with -0.01em letter spacing, converted to paths because GitHub can't load web fonts inside SVGs. It's the same face as Staypaw's, Sweepling's, Shybun's and Tattlebun's. Use the SVGs; don't re-typeset it. `scripts/make_lockup.py` rebuilds both lockups from the font and the mark, and its header says how. Rerun it whenever the mark changes.
- The house style: heavy black outlines and one hard shadow tone per colour.

Logo colours, never used for text: coral `#FF9B85`, coral shade `#EE7A64`, blush `#F25F55`, bandage `#FFE7BF`, sweat drop `#9AD8FF` and ink `#111111`. The wordmark is ink `#111111` on light backgrounds and paper `#F5F2EA` on dark ones.
