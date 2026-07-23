{{/*
Release-qualified resource name, shared by every resource in this chart so two
releases can coexist in one namespace without colliding. Truncated to 63 chars
for the DNS label limit; if the release name already contains the chart name
(e.g. `helm install endatix-api ./helm`) it is not repeated.
*/}}
{{- define "endatix-api.fullname" -}}
{{- if contains .Chart.Name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name .Chart.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{/*
Selector labels. These must be release-specific: without the instance label the
Service of one release would also match the pods of another. Used verbatim in
the Deployment selector, the pod template, and the Service selector — a
Deployment's selector is immutable, so all three must stay in sync.
*/}}
{{- define "endatix-api.selectorLabels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}
