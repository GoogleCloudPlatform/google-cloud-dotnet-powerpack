# Google Cloud Templates for dotnet new

These templates are a good starting place for building ASP.NET Core apps that run
on Google App Engine and Google Kubernetes Engine.

## Install (not yet available)

(When available), Install the templates by running `dotnet new -i Google.Cloud.Templates`.

## Creating from the templates

See the now installed templates by running `dotnet new -l`
There are two new templates available.

* GCP.ASP.NET.MVC (gcpmvc)
* GCP.ASP.NET.WebAPI (gcpwebapi)

Both will setup Stackdriver logging, error reporting, and traces.

See a list of available options for the gcpmvc template by running `dotnet new gcpmvc -h`

Create a new project from the gcpmvc template by running `dotnet new gcpmvc -n MyNewMvcProjectName`

## Development

The template projects can be compiled directly.
Use your faviorate IDE or code editor to update them.
