# ---- Build (SDK) ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files needed for restore
COPY Paperless/Paperless.Contracts/Paperless.Contracts.csproj   Paperless/Paperless.Contracts/
COPY Paperless/Paperless.OcrWorker/Paperless.OcrWorker.csproj   Paperless/Paperless.OcrWorker/

# Restore worker (brings in Messaging via ProjectReference)
RUN dotnet restore Paperless/Paperless.OcrWorker/Paperless.OcrWorker.csproj

# Copy full source tree
COPY Paperless/ Paperless/

# Publish worker
WORKDIR /src/Paperless/Paperless.OcrWorker
RUN dotnet publish -c Release -o /app

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install system packages required by Tesseract + Ghostscript
# - ghostscript: for PDF rasterization / page conversion
# - libleptonica-dev / libtesseract-dev: native libs for tesseract
# - tesseract-ocr: main tesseract binary;
# could use eg.: tesseract-ocr-eng -> provides English traineddata
RUN apt-get update \
 && apt-get install -y \
 --allow-unauthenticated \
 #--no-install-recommends \
       ghostscript \
       libleptonica-dev \
       libtesseract-dev \
       tesseract-ocr \
 && rm -rf /var/lib/apt/lists/*


 # create symlinks that some native wrappers expect.
RUN ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so
RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so
WORKDIR /app

# Where tessdata will be located
ENV TESSDATA_PREFIX=/usr/share

COPY --from=build /app .
ENTRYPOINT ["dotnet", "Paperless.OcrWorker.dll"]
