# Workflow Create API - Payload Example

## Endpoint
`POST /api/workflows`

## Full Workflow Creation (with WorkflowJson)

This endpoint supports the complete workflow JSON structure from the source API.

### Request Body

```json
{
  "name": "Workflow - 2026-03-12 05:49 PM",
  "description": "",
  "triggerType": 0,
  "triggerConfig": null,
  "publishImmediately": false,
  "workflowJson": {
    "blocks": [
      {
        "id": "YdjYySIbZyc650C9NrfNw",
        "left": 54,
        "top": 54,
        "width": 175,
        "height": 90,
        "color": "#2BCCBA",
        "icon": "mdi-flag",
        "type": "START",
        "settings": {
          "label": "Start",
          "initiateMode": "MANUAL",
          "initiateBy": ["USER"],
          "users": [],
          "groups": []
        }
      },
      {
        "id": "PMGQfaqLnvpWBlqNCSgMR",
        "left": 377,
        "top": 161,
        "width": 175,
        "height": 90,
        "color": "#A65EEA",
        "icon": "mdi-account-multiple",
        "type": "INTERNAL_ACTOR",
        "settings": {
          "label": "INTERNAL ACTOR 1",
          "initiateMode": "MANUAL",
          "initiateBy": ["USER"],
          "users": [],
          "groups": []
        }
      },
      {
        "id": "9ZxppKk0TSh3qLPy6Zg4x",
        "left": 597,
        "top": 201,
        "width": 175,
        "height": 90,
        "color": "#FC5C65",
        "icon": "mdi-flag",
        "type": "END",
        "settings": {
          "label": "END 2",
          "initiateMode": "MANUAL",
          "initiateBy": ["USER"],
          "users": [],
          "groups": []
        }
      }
    ],
    "rules": [
      {
        "id": "HSoczMNAbGVutRbjVUjL8",
        "left": 228.5,
        "top": 98.5,
        "fromBlockId": "YdjYySIbZyc650C9NrfNw",
        "toBlockId": "PMGQfaqLnvpWBlqNCSgMR"
      },
      {
        "id": "xXToWIpgeyxCpNFCp0DRD",
        "left": 551.5,
        "top": 205.5,
        "fromBlockId": "PMGQfaqLnvpWBlqNCSgMR",
        "toBlockId": "9ZxppKk0TSh3qLPy6Zg4x"
      }
    ],
    "settings": {
      "general": {
        "name": "Workflow - 2026-03-12 05:49 PM",
        "description": "",
        "initiateUsing": {
          "type": "DOCUMENT_FORM",
          "repositoryId": 0,
          "formId": 0
        },
        "ocr": {
          "required": false,
          "credit": 0
        }
      },
      "publish": {
        "publishOption": "DRAFT",
        "publishSchedule": "",
        "unpublishSchedule": ""
      }
    },
    "modifiedBlockIds": [],
    "blockStatus": 0,
    "mlPredictions": [],
    "hasSLASettings": 1,
    "initiateUserDomain": [],
    "masterFormIds": []
  }
}
```

### Response

```json
{
  "workflowId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isPublished": false,
  "repositoryId": null
}
```

## Simple Workflow Creation (without WorkflowJson)

For backward compatibility, you can still create a simple workflow:

```json
{
  "name": "Simple Workflow",
  "description": "A simple workflow",
  "triggerType": 0,
  "triggerConfig": null
}
```

## Notes

- The `workflowJson` field is optional. If provided, the full workflow creation flow is executed.
- If `workflowJson` is provided, the `name` and `description` from `workflowJson.settings.general` take precedence.
- The `publishOption` in `workflowJson.settings.publish` determines if the workflow is published immediately.
- UI positioning fields (`left`, `top`, `width`, `height`, `color`, `icon`) are preserved but not used in backend processing.

