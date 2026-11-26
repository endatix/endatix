{{- /*
The image tag: image.tag when set, else the chart's appVersion, which publish-docs-image.yml stamps
with the commit SHA the image was built from, so a chart deploys the image built with it. Empty
renders no Deployment.
*/ -}}
{{- define "site.imageTag" -}}
{{- .Values.image.tag | default .Chart.AppVersion -}}
{{- end }}
