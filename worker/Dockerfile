FROM mcr.microsoft.com/dotnet/sdk:8.0

RUN apt-get update && apt-get install -y \
    python3 python3-pip \
    && rm -rf /var/lib/apt/lists/*

RUN pip3 install flask --break-system-packages
RUN mkdir -p /app/csharp_template && \
    dotnet new console -o /app/csharp_template --force && \
    cd /app/csharp_template && \
    dotnet restore && \
    dotnet build -c Release
WORKDIR /app
COPY worker.py .
EXPOSE 5000
CMD ["python3", "worker.py"]