# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base


WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy project files (based on your actual structure)
COPY ["Server/Programmin2_classroom.Server.csproj", "Server/"]
COPY ["Client/Programmin2_classroom.Client.csproj", "Client/"]
COPY ["Shared/Programmin2_classroom.Shared.csproj", "Shared/"]

# Restore dependencies
RUN dotnet restore "Server/Programmin2_classroom.Server.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/Server"
RUN dotnet build "Programmin2_classroom.Server.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Programmin2_classroom.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Create the runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directory for SQLite database
RUN mkdir -p /app/data

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "Programmin2_classroom.Server.dll"]