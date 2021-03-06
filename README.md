Active development moved under[Project BlueBird](https://github.com/project-bluebird).

# Twitcher
Front-end for [Bluebird](https://github.com/alan-turing-institute/bluebird) for monitoring the simulation.

Wikipedia [article on birdwatching](https://en.wikipedia.org/wiki/Birdwatching) defines *twitcher* as:
> The term twitcher, sometimes misapplied as a synonym for birder, is reserved for those who travel long distances to see a rare bird that would then be ticked, or counted on a list.

## Running Twitcher

The recommended way to run Twitcher is via a docker container. Simply clone the repository and run

```
docker-compose up --build
```

Then go to [http://localhost:8080/](http://localhost:8080/) in your browser. The visualisation is tested in Chrome.

Twitcher assumes that [Bluesky](https://github.com/alan-turing-institute/bluesky/) and [Bluebird](https://github.com/alan-turing-institute/bluebird/) are already running on the same machine.

## Developing Twitcher

Twitcher is built using .NET Core and Fable, then compiled to Javascript. 

The recommended method of developing is inside a docker container, using [VS Code Remote - Containers extension](https://code.visualstudio.com/docs/remote/containers). The folder `.devcontainer` contains the required settings and the `Dockerfile` of the development machine with all dependencies installed. To start developing, install the VS Code extension and open the project in a dev container.

### Overview of requirements

If you want to develop the app outside of the dev container, here is the list of requirements (see also `.devcontainer/Dockerfile`):

* [dotnet SDK](https://www.microsoft.com/net/download/core) 2.1 or higher
* [node.js](https://nodejs.org) with [yarn](https://yarnpkg.com/lang/en/)
* [mono](https://www.mono-project.com/docs/getting-started/install/) (or [.NET Framework](https://dotnet.microsoft.com/download/dotnet-framework)) for restoring dependencies
* For development: Visual Studio Code with [Ionide plugin](http://ionide.io/).


### Building and running the app

#### In development mode

*If you are using Windows replace `./fake.sh` by `fake.cmd`*

1. Run: `./fake.sh build -t Watch`
2. Go to [http://localhost:8080/](http://localhost:8080/)

*On Unix you may need to run `chmod a+x fake.sh`*

In development mode, we activate:

- [Hot Module Replacement](https://fable-elmish.github.io/hmr/), modify your code and see the change on the fly

#### Build for production

*If you are using Windows replace `./fake.sh` by `fake.cmd`*

1. Run: `./fake.sh build`
2. All the files needed for deployment are under the `output` folder.


### Notes

By default, the app displays a default sector outline. To load the training sector 31 outline, put the `nats-sector.json` file from Sharepoint into the `static` folder.
