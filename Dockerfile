FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /w
COPY . /w
RUN dotnet publish biorand-classic -c release -o /out -p:PublishSingleFile=true

FROM alpine
RUN apk add --no-cache libstdc++
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
COPY --from=build /out/biorand-classic /usr/bin/biorand-classic
RUN biorand-classic --version

CMD /usr/bin/biorand-classic
