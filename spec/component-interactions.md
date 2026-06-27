# DataRetriever Component Interactions

This document shows the major components in the template and how they interact at runtime.

Legend:

- Solid arrows are runtime calls or data flow.
- Dashed arrows are dependency-injection choices.
- `Application` owns orchestration and business flow.
- `Infrastructure` and `Simulators` implement application-owned boundaries.
- `Reporting` and `Monitoring` are reusable pieces that can be used together or separately.

## 1. Project Boundary View

```mermaid
flowchart LR
    Caller["Caller / scheduler / manual request"]

    subgraph Api["DataRetriever.Api"]
        Program["Program.cs"]
        EndpointRoutes["Endpoint route mapping"]
        RunEndpoint["RunDataRetrievalEndpoint"]
        StatusEndpoint["GetDataRetrievalStatusEndpoint"]
        ApiComposition["ServiceCollectionExtensions"]
        AdapterMode["AdapterMode"]
        SingleRunGuard["SingleRunGuard"]
    end

    subgraph Application["DataRetriever.Application"]
        Orchestrator["DataRetrievalOrchestrator"]
        StepRunner["StepRunner"]
        RunFinalizer["RunReportFinalizer"]
        SummaryBuilder["DataRetrievalReportSummaryBuilder"]
        Step4TableBuilder["Step4ReportTableBuilder"]

        subgraph Step1["Step1Load slice"]
            Step1Loader["Step1Loader"]
            Step1Mapper["Step1Mapper"]
            Step1Validator["Step1Validator"]
            IStep1Source["IStep1SourceClient"]
        end

        subgraph Step2["Step2Load slice"]
            Step2Loader["Step2Loader"]
            Step2Mapper["Step2ResponseMapper"]
            Step2Selector["Step2Selector"]
            IStep2Source["IStep2SourceClient"]
        end

        subgraph Step3["Step3Load slice"]
            Step3Loader["Step3Loader"]
            Step3RequestMapper["Step3RequestMapper"]
            Step3ResponseMapper["Step3ResponseMapper"]
            Step3Validator["Step3ResponseValidator"]
            ExternalIdNormalizer["ExternalId2Normalizer"]
            IStep3Source["IStep3SourceClient"]
        end

        subgraph Step4["Step4Persist slice"]
            Step4Persister["Step4Persister"]
            Step4RequestMapper["Step4RequestMapper"]
            IStep4Sink["IStep4SinkClient"]
        end
    end

    subgraph Execution["DataRetriever.Execution"]
        IStep["IStep<TInput,TOutput>"]
        Result["StepExecutionResult<T>"]
        Issue["StepIssue + DiagnosticContext"]
        RunContext["RunContext"]
        StatusEnums["RunStatus / StepExecutionStatus"]
        Counters["StepCounter"]
    end

    subgraph Monitoring["DataRetriever.Monitoring"]
        ProcessingTracker["IProcessingTracker"]
        RunInstrumentation["IRunInstrumentation"]
        InstrumentationInfo["IInstrumentationInfo"]
        Snapshot["ProcessingRunSnapshot"]
    end

    subgraph Reporting["DataRetriever.Reporting"]
        ReportBuilder["RunReportBuilder"]
        ReportModel["RunReport"]
        ReportPublisher["IRunReportPublisher"]
        EmailFormatter["IRunReportEmailFormatter"]
        RazorFormatter["RazorRunReportEmailFormatter"]
        EmailTemplates["Razor email templates"]
    end

    subgraph Infrastructure["DataRetriever.Infrastructure"]
        RealStep1["Step1SourceClient"]
        RealStep2["Step2SourceClient"]
        RealStep3Adapter["Step3SourceClient"]
        RealStep3Client["Step3ExternalClient"]
        RealStep4["Step4SinkClient"]
        HealthChecks["Step health checks"]
        EmailPublisher["EmailRunReportPublisher"]
        MailKitSender["MailKitEmailSender"]
    end

    subgraph Simulators["DataRetriever.Simulators"]
        SimStep1["Step1SourceSimulator"]
        SimStep2["Step2SourceSimulator"]
        SimStep3["Step3SourceSimulator"]
        SimStep4["Step4SinkSimulator"]
        SimPublisher["SimulatedEmailRunReportPublisher"]
        SeedData["SimulatorSeedData"]
    end

    subgraph External["External systems"]
        Source1["Configured data source"]
        Source2["Related data source"]
        Source3["Amount/source API"]
        Sink["Persistence store"]
        Smtp["SMTP server"]
    end

    Caller --> RunEndpoint
    Caller --> StatusEndpoint
    Program --> EndpointRoutes
    EndpointRoutes --> RunEndpoint
    EndpointRoutes --> StatusEndpoint
    ApiComposition --> AdapterMode
    RunEndpoint --> SingleRunGuard
    RunEndpoint --> Orchestrator
    StatusEndpoint --> ProcessingTracker

    Orchestrator --> StepRunner
    Orchestrator --> Step1Loader
    Orchestrator --> Step2Loader
    Orchestrator --> Step3Loader
    Orchestrator --> Step4Persister
    Orchestrator --> RunFinalizer

    StepRunner --> Result
    StepRunner --> Issue
    StepRunner --> RunInstrumentation
    StepRunner --> Counters

    Step1Loader --> IStep1Source
    Step1Loader --> Step1Mapper
    Step1Loader --> Step1Validator
    Step2Loader --> IStep2Source
    Step2Loader --> Step2Mapper
    Step2Loader --> Step2Selector
    Step3Loader --> IStep3Source
    Step3Loader --> Step3RequestMapper
    Step3Loader --> Step3ResponseMapper
    Step3Loader --> Step3Validator
    Step3RequestMapper --> ExternalIdNormalizer
    Step3ResponseMapper --> ExternalIdNormalizer
    Step4Persister --> IStep4Sink
    Step4Persister --> Step4RequestMapper

    IStep --> Result
    Result --> Issue
    Result --> StatusEnums
    Orchestrator --> RunContext

    RunFinalizer --> SummaryBuilder
    RunFinalizer --> Step4TableBuilder
    RunFinalizer --> ReportBuilder
    RunFinalizer --> ReportPublisher
    ReportBuilder --> ReportModel
    ReportPublisher --> EmailPublisher
    EmailPublisher --> EmailFormatter
    EmailFormatter --> RazorFormatter
    RazorFormatter --> EmailTemplates
    EmailPublisher --> MailKitSender

    ProcessingTracker --> Snapshot
    RunInstrumentation --> InstrumentationInfo

    ApiComposition -. "Real mode" .-> RealStep1
    ApiComposition -. "Real mode" .-> RealStep2
    ApiComposition -. "Real mode" .-> RealStep3Adapter
    ApiComposition -. "Real mode" .-> RealStep4
    ApiComposition -. "Real mode" .-> EmailPublisher
    ApiComposition -. "Simulator mode" .-> SimStep1
    ApiComposition -. "Simulator mode" .-> SimStep2
    ApiComposition -. "Simulator mode" .-> SimStep3
    ApiComposition -. "Simulator mode" .-> SimStep4
    ApiComposition -. "Simulator mode" .-> SimPublisher

    RealStep1 --> Source1
    RealStep2 --> Source2
    RealStep3Adapter --> RealStep3Client
    RealStep3Client --> Source3
    RealStep4 --> Sink
    MailKitSender --> Smtp

    SimStep1 --> SeedData
    SimStep2 --> SeedData
    SimStep3 --> SeedData
    SimStep4 --> SeedData
```

## 2. Runtime Request Sequence

```mermaid
sequenceDiagram
    autonumber

    actor Caller
    participant Endpoint as RunDataRetrievalEndpoint
    participant Guard as SingleRunGuard
    participant Orchestrator as DataRetrievalOrchestrator
    participant Tracker as IProcessingTracker
    participant StepRunner as StepRunner
    participant Step1 as Step1Loader
    participant Step2 as Step2Loader
    participant Step3 as Step3Loader
    participant Step4 as Step4Persister
    participant Finalizer as RunReportFinalizer
    participant Publisher as IRunReportPublisher

    Caller->>Endpoint: POST run request
    Endpoint->>Endpoint: validate filters
    Endpoint->>Guard: TryEnterAsync()

    alt another run is active
        Guard-->>Endpoint: no lease
        Endpoint-->>Caller: 409 Conflict
    else run can start
        Guard-->>Endpoint: lease
        Endpoint->>Orchestrator: RunAsync(options)
        Orchestrator->>Orchestrator: create RunContext with Guid RunId
        Orchestrator->>Tracker: ForRun(runId)
        Orchestrator->>Tracker: record Running

        Orchestrator->>StepRunner: Execute Step 1
        StepRunner->>Step1: ExecuteAsync(Step1Input)
        Step1-->>StepRunner: StepExecutionResult<Step1Output>
        StepRunner->>Tracker: record Step 1 counters/status
        StepRunner-->>Orchestrator: Step 1 result

        alt Step 1 has fatal error or no usable output
            Orchestrator->>Finalizer: Finish failed report
        else Step 1 can continue
            Orchestrator->>StepRunner: Execute Step 2 with Step1Output
            StepRunner->>Step2: ExecuteAsync(Step1Output)
            Step2-->>StepRunner: StepExecutionResult<Step2Output>
            StepRunner->>Tracker: record Step 2 counters/status
            StepRunner-->>Orchestrator: Step 2 result
        end

        alt Step 2 has fatal error or no usable output
            Orchestrator->>Finalizer: Finish failed report
        else Step 2 can continue
            Orchestrator->>StepRunner: Execute Step 3 with Step2Output
            StepRunner->>Step3: ExecuteAsync(Step2Output)
            Step3-->>StepRunner: StepExecutionResult<Step3Output>
            StepRunner->>Tracker: record Step 3 counters/status
            StepRunner-->>Orchestrator: Step 3 result
        end

        alt Step 3 has fatal error or no usable output
            Orchestrator->>Finalizer: Finish failed report
        else Step 3 can continue
            Orchestrator->>StepRunner: Execute Step 4 with Step3Output
            StepRunner->>Step4: ExecuteAsync(Step3Output)
            Step4-->>StepRunner: StepExecutionResult<Step4Output>
            StepRunner->>Tracker: record Step 4 counters/status
            StepRunner-->>Orchestrator: Step 4 result
            Orchestrator->>Finalizer: Finish success or failed report
        end

        Finalizer->>Tracker: record final run status
        Finalizer->>Finalizer: build RunReport
        Finalizer->>Publisher: PublishAsync(report)
        Publisher-->>Finalizer: published or logged
        Finalizer-->>Orchestrator: RunReport
        Orchestrator-->>Endpoint: RunReport
        Endpoint-->>Caller: 200 OK with report
    end
```

## 3. Step Data Flow

```mermaid
flowchart TB
    Request["RunDataRetrievalRequest<br/>optional currency OR internal ids"]
    Options["DataRetrievalRunOptions"]

    subgraph S1["Step 1: load configured data"]
        S1Input["Step1Input"]
        S1Client["IStep1SourceClient"]
        S1Dto["Step1Dto<br/>source row"]
        S1Mapper["Step1Mapper"]
        S1Validator["Step1Validator"]
        S1Output["Step1Output<br/>Step1OutputRecord[]"]
    end

    subgraph S2["Step 2: load related data"]
        S2Client["IStep2SourceClient"]
        S2Dto["Step2ResponseDto<br/>source row"]
        S2Mapper["Step2ResponseMapper"]
        S2Selector["Step2Selector"]
        S2Output["Step2Output<br/>Step2OutputRecord[]"]
    end

    subgraph S3["Step 3: load amount/source data"]
        S3RequestMapper["Step3RequestMapper"]
        S3Request["Step3RequestDto"]
        S3Client["IStep3SourceClient"]
        S3Response["Step3ResponseDto<br/>Step3ResponseItemDto[]"]
        S3ResponseMapper["Step3ResponseMapper"]
        S3Validator["Step3ResponseValidator"]
        S3Output["Step3Output<br/>Step3OutputRecord[]"]
    end

    subgraph S4["Step 4: persist final data"]
        S4RequestMapper["Step4RequestMapper"]
        S4Request["Step4RequestDto[]"]
        S4Sink["IStep4SinkClient"]
        S4Result["Step4PersistResult[]<br/>row-level outcomes"]
        S4Output["Step4Output<br/>persisted records"]
    end

    Request --> Options
    Options --> S1Input
    S1Input --> S1Client
    S1Client --> S1Dto
    S1Dto --> S1Mapper
    S1Mapper --> S1Validator
    S1Validator --> S1Output

    S1Output --> S2Client
    S2Client --> S2Dto
    S2Dto --> S2Mapper
    S2Mapper --> S2Selector
    S2Selector --> S2Output

    S2Output --> S3RequestMapper
    S3RequestMapper --> S3Request
    S3Request --> S3Client
    S3Client --> S3Response
    S3Response --> S3Validator
    S3Response --> S3ResponseMapper
    S3Validator --> S3Output
    S3ResponseMapper --> S3Output

    S3Output --> S4RequestMapper
    S4RequestMapper --> S4Request
    S4Request --> S4Sink
    S4Sink --> S4Result
    S4Result --> S4Output
```

The important template rule is that each step owns its own local models and mappers. The next step consumes the previous step's output as its input. External DTOs stay inside the relevant step slice.

## 4. Adapter Selection

```mermaid
flowchart LR
    Config["Configuration<br/>AdapterMode"]
    ApiComposition["DataRetriever.Api<br/>ServiceCollectionExtensions"]

    subgraph AppBoundaries["Application-owned boundaries"]
        B1["IStep1SourceClient"]
        B2["IStep2SourceClient"]
        B3["IStep3SourceClient"]
        B4["IStep4SinkClient"]
        BP["IRunReportPublisher"]
    end

    subgraph Real["Real adapter registration"]
        R1["Infrastructure Step1SourceClient"]
        R2["Infrastructure Step2SourceClient"]
        R3["Infrastructure Step3SourceClient<br/>wraps Step3ExternalClient"]
        R4["Infrastructure Step4SinkClient"]
        RP["EmailRunReportPublisher<br/>MailKitEmailSender"]
    end

    subgraph Sim["Simulator adapter registration"]
        S1["Step1SourceSimulator"]
        S2["Step2SourceSimulator"]
        S3["Step3SourceSimulator"]
        S4["Step4SinkSimulator"]
        SP["SimulatedEmailRunReportPublisher<br/>or real email for local SMTP test"]
    end

    Config --> ApiComposition
    ApiComposition -. "Real" .-> Real
    ApiComposition -. "Simulator" .-> Sim

    R1 -. implements .-> B1
    R2 -. implements .-> B2
    R3 -. implements .-> B3
    R4 -. implements .-> B4
    RP -. implements .-> BP

    S1 -. implements .-> B1
    S2 -. implements .-> B2
    S3 -. implements .-> B3
    S4 -. implements .-> B4
    SP -. implements .-> BP

    B1 --> Step1["Step1Loader"]
    B2 --> Step2["Step2Loader"]
    B3 --> Step3["Step3Loader"]
    B4 --> Step4["Step4Persister"]
    BP --> Finalizer["RunReportFinalizer"]
```

The application layer does not know which implementation it receives. Switching from simulator-backed execution to real dependencies is a composition decision in the API host.

## 5. Monitoring, Logging, Issues, and Report Flow

```mermaid
flowchart TB
    Step["Step implementation"]
    StepResult["StepExecutionResult<br/>status, counters, issues, output"]
    Issue["StepIssue<br/>severity, message, DiagnosticContext"]
    Runner["StepRunner"]
    Logger["ILogger"]
    InstrumentationWriter["RunInstrumentationWriter"]
    RunInstrumentation["IRunInstrumentation"]
    Tracker["InMemoryProcessingTracker"]
    StatusEndpoint["GET status endpoint"]

    Finalizer["RunReportFinalizer"]
    ReportBuilder["RunReportBuilder"]
    SummaryBuilder["DataRetrievalReportSummaryBuilder"]
    TableBuilder["Step4ReportTableBuilder"]
    RunReport["RunReport"]
    Publisher["IRunReportPublisher"]

    Step --> StepResult
    StepResult --> Issue
    StepResult --> Runner
    Runner --> Logger
    Runner --> InstrumentationWriter
    InstrumentationWriter --> RunInstrumentation
    RunInstrumentation --> Tracker
    StatusEndpoint --> Tracker

    Runner --> Finalizer
    Finalizer --> SummaryBuilder
    Finalizer --> TableBuilder
    Finalizer --> ReportBuilder
    ReportBuilder --> RunReport
    SummaryBuilder --> RunReport
    TableBuilder --> RunReport
    RunReport --> Publisher
```

This is why logging, monitoring, and reporting are related but not the same thing:

- Logging is immediate operational trace output.
- Monitoring is the current/latest run state exposed while the service is running.
- Reporting is the final structured artifact for a run, including issues and tables.

All three can use the same `StepIssue` and `DiagnosticContext`, so the same identifier context appears in logs and reports without forcing the systems to be physically combined.

## 6. Email Report Flow

```mermaid
flowchart LR
    Report["RunReport"]
    Publisher["EmailRunReportPublisher"]
    Formatter["IRunReportEmailFormatter"]
    RazorFormatter["RazorRunReportEmailFormatter"]

    subgraph Templates["Razor email templates"]
        MainTemplate["RunReportEmailTemplate.razor"]
        IssuesTable["IssuesTable.razor"]
        ReportTable["ReportTable.razor"]
    end

    Message["EmailMessage"]
    Sender["IEmailSender"]
    MailKit["MailKitEmailSender"]
    Smtp["SMTP server<br/>smtp4dev locally or real SMTP later"]

    Report --> Publisher
    Publisher --> Formatter
    Formatter --> RazorFormatter
    RazorFormatter --> MainTemplate
    MainTemplate --> IssuesTable
    MainTemplate --> ReportTable
    RazorFormatter --> Message
    Publisher --> Sender
    Sender --> MailKit
    MailKit --> Smtp
```

The common email formatter owns layout. Each service owns the tables it adds to `RunReport.Tables`, such as the persisted-record table in this template.

Current email order:

1. Header and run status.
2. Errors, only when errors exist.
3. Warnings, only when warnings exist.
4. Optional stats, controlled by `RunReportEmail:DisplayStats`.
5. Report tables, such as persisted rows.

## 7. Dependency Direction Summary

```mermaid
flowchart BT
    Infrastructure["Infrastructure"]
    Simulators["Simulators"]
    Api["Api"]
    Application["Application"]
    Execution["Execution"]
    Reporting["Reporting"]
    Monitoring["Monitoring"]

    Api --> Application
    Api --> Infrastructure
    Api --> Simulators
    Api --> Reporting
    Api --> Monitoring

    Application --> Execution
    Application --> Reporting
    Application --> Monitoring

    Infrastructure --> Application
    Infrastructure --> Reporting
    Infrastructure --> Execution

    Simulators --> Application
    Simulators --> Reporting
    Simulators --> Monitoring

    Reporting --> Execution
    Monitoring --> Execution
```

The key design point is that infrastructure points inward to application interfaces, while the API host composes everything. The application flow remains testable because the loaders/persister depend on source/sink interfaces, not concrete databases, HTTP clients, SMTP, or simulators.
