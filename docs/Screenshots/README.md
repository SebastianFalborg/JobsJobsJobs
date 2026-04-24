# Package screenshots

These PNGs are referenced from `docs/README_nuget.md` via raw GitHub URLs so nuget.org and the Umbraco Marketplace render them directly from this repo. Keep the filenames stable — changing a name silently breaks the rendered README on already-published package versions.

## Files in use

| Filename | Shown in README section | Also referenced from |
| --- | --- | --- |
| `Dashboard overview.png` | Top of README (hero) | `umbraco-marketplace.json` |
| `History tab.png` | `### History tab` | `umbraco-marketplace.json` |
| `Stoppable CRON job.png` | `## Stop support and cooperative cancellation` | `umbraco-marketplace.json` |

The folder is named `Screenshots` with a capital `S` — GitHub URLs are case-sensitive, so do not rename to lowercase or the URLs in `README_nuget.md` and `umbraco-marketplace.json` will 404 on nuget.org and the marketplace. Spaces in filenames are URL-encoded as `%20` in both files; do not rename the files without updating both reference points.

## Tips

- Shoot at a stable backoffice width (around 1440 px) so the layout matches what a typical user will see.
- Light theme is fine; the marketplace does not theme the embedded images.
- PNG, reasonable compression. Avoid JPG (marketplace renders it blurry).
- Drop updated versions in place and push; nuget.org caches per-version, so the *already-published* package version keeps its old image until you publish a new version. On the package page and on GitHub you will see the new image immediately.

## Publish order

When shipping a new version that references a new screenshot:

1. Commit the PNG to `main`.
2. Push to GitHub.
3. Only then pack and publish the NuGet package.

If you publish before the PNGs are on `main`, the README on nuget.org will show broken image icons for that version until the next release.
