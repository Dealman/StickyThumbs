<img src="./StickyThumbs/StickyThumbs.ico" alt="StickyThumbs Icon" width="180" align="left"/><h1 align="center">StickyThumbs</h1>

<p align="center">Creates sticky live-preview thumbnails of running applications.<br>The thumbnails remain topmost, can be moved around, resized, zoomed and panned.</p>
<p>&nbsp;</p>

## About

StickyThumbs utilizes the [Win32 DWM API](https://learn.microsoft.com/en-us/windows/win32/dwm/reference) to create live-preview thumbnails of running processes with a valid window. Those are the same kind of thumbnails you see when you hover the mouse over an application in the Windows task bar.

However, with this program they act as separate windows which can be customized. They also remain topmost, so they should render on top of other programs as well as games running in borderless windowed.

### How to Use

- The program is installed via [ClickOnce](https://en.wikipedia.org/wiki/ClickOnce), which means you'll easily be able to update the program whenever a new version is available.
- Upon starting the program, you'll be presented by some running processes. Double-click either to open a new thumbnail of that process.
- Every thumbnail has a overlay which can be shown by moving the mouse towards the top of the thumbnail window.
- If you double-click the thumbnail, it will bring the process window to the foreground.
- You can close the thumbnail either via the overlay or pressing delete *(thumbnail needs focus, click it one)*.

## Screenshot
![image](https://github.com/Dealman/StickyThumbs/assets/7038067/f827960c-d652-463e-8510-47a7796488b4)


## Installation

This program is intended for use with Windows 10 and above, and requires [.NET 7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) which CickOnce should install automatically(maybe?).

To intall, click [this link](https://Dealman.github.io/StickyThumbs/StickyThumbs.application) and follow the ClickOnce Installation prompt.

## License

StickyThumbs uses the MIT License.

## Credits

Thanks to [MahApps.Metro](https://github.com/MahApps/MahApps.Metro) for their amazing toolit<br>
Thanks to [Jan Jones](https://janjones.me/) for his great [ClickOnce Post](https://janjones.me/posts/clickonce-installer-build-publish-github/)<br>
Icons by [Freepik á Flaticon](https://www.flaticon.com/authors/freepik)
