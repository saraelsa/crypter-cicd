FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /source/

RUN dotnet tool install --global dotnet-references
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

COPY *.sln ./
COPY */*.csproj ./

RUN dotnet-references fix --entry-point ./Crypter.sln --working-directory ./ --remove-unreferenced-project-files
RUN dotnet restore Crypter.API --runtime linux-x64

COPY ./ ./
RUN dotnet publish Crypter.API --runtime linux-x64 --no-self-contained --no-restore --configuration release --output /app/
RUN dotnet-ef migrations bundle --target-runtime linux-x64 --no-build --project Crypter.API --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Crypter.API.dll"]
