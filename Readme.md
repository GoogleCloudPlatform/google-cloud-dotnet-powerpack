# Google Cloud .NET PowerPack

This repository contains small NuGet packages and project templates helpful for
building apps that run on Google Cloud Platform.

## NuGet packages

### Google.Cloud.Templates

These templates are a good starting place for building ASP.NET Core apps that run on [Google App Engine][GAE] and
[Google Kubernetes Engine][GKE].
(When available), Install the templates by running `dotnet new -i Google.Cloud.Templates`,
then create a new ASP.NET Core MVC project by running `dotnet new gcpmvc -n MyMvcProjectName`.

See the [Templates Readme][Google.Cloud.Templates.Readme] for more information.

## Contributing

We'd love to accept your patches and contributions to this project.
See [Contributing.md][Contributing.md] for more information.

## License

All code in this repository falls under the Apache 2.0 License. See [LICENSE][LICENSE] for more information.


[Google.Cloud.Templates.Readme]: Google.Cloud.Templates/Readme.md
[Contributing.md]: Contributing.md
[GKE]: https://cloud.google.com/kubernetes-engine
[GAE]: https://cloud.google.com/appengine
[LICENSE]: LICENSE
