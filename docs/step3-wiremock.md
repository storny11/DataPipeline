# Step 3 WireMock Simulator

Use this mock when you want to capture real Step 3 HTTP responses once, then replay them locally without calling the real Step 3 service.

The mock runs from:

```powershell
docker-compose.step3-simulator.yml
```

Captured files are written under:

```text
captures/wiremock/step3
```

That folder is ignored by git through the existing `captures/` rule.

## Capture Real Step 3 Data

Start WireMock in proxy/record mode. Replace the upstream URL with the real Step 3 service base URL.

```powershell
$env:WIREMOCK_OPTIONS = "--verbose --proxy-all=https://your-real-step3-service --record-mappings --supported-proxy-encodings=identity"
docker compose -f docker-compose.step3-simulator.yml up
```

Point the service Step 3 client at WireMock:

```text
Step3SourceClient:BaseUrl = http://localhost:8080
```

Run the service and trigger the data retrieval run.

During this run:

```text
DataRetriever -> WireMock localhost:8080 -> real Step 3 service
```

WireMock forwards the requests to the real service and records matching response files under:

```text
captures/wiremock/step3/mappings
captures/wiremock/step3/__files
```

Stop WireMock with `Ctrl+C` when the capture is complete.

## Replay Captured Step 3 Data

Clear the capture/proxy options:

```powershell
Remove-Item Env:WIREMOCK_OPTIONS -ErrorAction SilentlyContinue
```

Start WireMock in replay mode:

```powershell
docker compose -f docker-compose.step3-simulator.yml up
```

Keep the service Step 3 client pointed at WireMock:

```text
Step3SourceClient:BaseUrl = http://localhost:8080
```

Run the service again.

During this run:

```text
DataRetriever -> WireMock localhost:8080 -> captured files
```

WireMock serves the responses from the saved mappings and should not call the real Step 3 service.

## Notes

The replayed request must match the captured request. If the generated client changes the HTTP method, path, query string, headers used for matching, or request body shape, WireMock may not find the saved mapping.

If the service also runs inside Docker Compose on the same network, use the service name instead of localhost:

```text
Step3SourceClient:BaseUrl = http://step3-simulator:8080
```
