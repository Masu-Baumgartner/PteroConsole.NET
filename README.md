## PteroConsole.Net
### A simple library for connecting to a pterodactyl server console



#### How to use

First create a **PterodactylConsole** object

*panelUrl: the url of your pterodactyl panel e.g. 'https://pterodactyl.file.properties/''*

*clientKey: an api key created by a user with access to the server you want to connect*

*serverUuid: the uuid of the server. this cloud be either the long or short version, e.g. '6c1c16ae', '6c1c16ae-abbe-495b-a96d-9b83e5018cbf'*
````csharp
var console = PterodactylConsole.Create(panelUrl, clientKey, serverUuid);
````

Then you should configure the events so you can listen to things sent from the server

````csharp
console.OutputReceived += (sender, msg) =>
{
    Console.WriteLine($"OUTPUT: {msg}");
};

````

The last thing you have to do is to start connecting to console
````csharp
console.Connect();
````

If you want to close the console you just call the Dispose function

`````csharp
console.Dispose();
`````

#### License

Copyright 2022 Marcel Baumgartner

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

