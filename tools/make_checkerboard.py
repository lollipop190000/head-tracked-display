"""Generate an A4-landscape-friendly 9x6-inner-corner, 24 mm square board."""
from pathlib import Path

OUT = Path(__file__).resolve().parent / "checkerboard_9x6_24mm.svg"
SQUARE_MM = 24
COLUMNS, ROWS = 10, 7


def main() -> None:
    parts = [
        '<svg xmlns="http://www.w3.org/2000/svg" width="240mm" height="168mm" viewBox="0 0 240 168">',
        '<rect width="240" height="168" fill="white"/>',
    ]
    for row in range(ROWS):
        for column in range(COLUMNS):
            if (row + column) % 2 == 0:
                parts.append(f'<rect x="{column*SQUARE_MM}" y="{row*SQUARE_MM}" '
                             f'width="{SQUARE_MM}" height="{SQUARE_MM}" fill="black"/>')
    parts.append('</svg>')
    OUT.write_text("\n".join(parts) + "\n", encoding="utf-8")
    print(OUT)


if __name__ == "__main__":
    main()
