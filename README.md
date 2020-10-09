# Nover
A now playing application for Spotify with specific advantages over alternatives such as [Snip](https://github.com/dlrudie/Snip). Advantages include faster loading, adjustable refresh rate, support for local file album covers, and partial offline functionality, all without any authentication through Spotify.

# Usage
After running the program for the first time, the application will automatically create a folder in your Documents folder, which will contain Nover.txt, the text file that will display the information about the song playing, cover.jpg, which will be the album cover, and settings.ini, where you can adjust the settings for the program. Simply set up OBS or a similar program to read from the txt and jpg files, like you would with other Now Playing programs, and you're go to go!

# Settings
You can configure the location of your local files, which will allow Nover to load the album covers from the files if possible. All you have to do is enter the path of the folder that contains the songs in the .ini file. **Songs must be top level in the directory**

You can have a custom image appear whenever there is no album cover available, for example when you're loading a local file with no album cover or if you're offline. Simply enter the path of the image in the .ini file.

You can adjust the duration between each update of the song by inputting your desired length in milliseconds into the .ini.

You can change the format that the text in Nover.txt is in by changing the text format in the .ini to your desired configuration. Just put {title} and {artist} where you want the title and artist respectively to be.
