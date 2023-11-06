![image](https://user-images.githubusercontent.com/98792/229350512-f74e4f52-9f70-4a86-874a-3cb07f25bb17.png)

## webDemoExe

wrap your web demo into a windows exe format, just like a native demo.

[download](https://github.com/pandrr/WebDemoExe/releases)

### about

- it does not use electron, size is ~0.5mb
- it will use edge to display your demo. edge is a chromium based browser, which is basically chrome
- shows a little start dialog (only fullscreen option right now, more in the future hopefully)

### how

- download the zip file from the releases section 
- put your static html/js files into the demo subfolder
- edit webdemoexe.xml and change the title
- rename webdemoexe.exe to your demo name
- add `<autostart/>` into the config to not show the dialog at all and start directly

- if the url contains "webdemoexe_exit" it will exit, e.g. use window.location.hash="webdemoexe_exit"

### technical
- exe is not signed, still have to click "run anyway", like with most demos
- webdemoexe uses [webview2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/) and creates a virtual host from the demo subfolder to run your demo
- escape to close is handled by webdemoexe
- no gesture is needed to auto play audio, if you normally display a play button, make sure it only shows when audiocontext stats is not "running"...

### ideas
- currently has no resolution selection, not sure how this is possible with wpf etc.
- in the future the dialog could show link to website/online version and maybe a little teaser image...
- there should be way to exit the app from js / in electron we always used `window.close()` not possible with webview2 afaik

### misc

thanks to kb for helping with initial setup!

any help is appreciated. i am not a windows developer, i hope everything here is not too wrong.

