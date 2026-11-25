# Settings and telemetry

This file tells how to add a setting, a trace, or a metric.

## Settings

An options class has a `SectionName` and a validator. Register them in
`Configuration/Extensions/ServiceExtensions.cs`. Then the server does not
start with incorrect settings, and `check` shows them.

If a self-hoster needs a setting, also write about it in the chart
([charts](https://github.com/EchoPhase-Project/charts)).

## Telemetry

Traces and metrics are on only when an OTLP endpoint is set. For new traces
or metrics, use the current source and meter, or register a new one in
`TelemetryExtensions.cs`. Metric names and attribute names start with
`echophase.`.
