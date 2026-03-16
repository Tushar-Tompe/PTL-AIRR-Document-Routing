# SIRM Document Routing (PTL-AIRR)

Windows service that automates ingestion of scanned/received TIFF documents: recognizes document types, extracts key values from filenames, indexes documents in **Doclink**, and moves files into validation or unknown folders for downstream workflows.

---

## Solution structure

| Project | Description |
|--------|-------------|
| **SirmEdmImageProcessing** | Core library: file monitoring, document recognition, Doclink indexing, validation XML, unknown-folder routing. Can run as a console app for testing. |
| **SirmEdmImageProcessingWS** | Windows Service that runs in the background and triggers the core logic on a configurable timer (e.g. every 15 seconds with 0.25 minutes). |
| **TestConsole** | Console host for running the same logic without installing the service (development/testing). |
| **SirmEdmProcessingSetup** | Installer project for deploying the Windows Service. |

---

## How it works

1. **Timer** (Windows Service) fires at the configured interval and calls `ImageProcessing.StartProcessing()`.
2. **Queue folder** is scanned for `*.tif` files (path set in config, e.g. `QueueFolder`).
3. **Per file:**  
   - Filename is parsed for document type, key value, and optional invoice number (e.g. `KeyValue_InvoiceNo-DocType-TEST.TIF`).  
   - Document type is matched against the **Common** database cache (`ListSirmDocumentTypeInfo`).  
   - **If recognized:** Doclink login → create document → set properties → save → optional workflow placement → write validation XML to the Validation folder → delete original from queue.  
   - **If not recognized:** Collator path is looked up by location (or default unknown folder is used); file is copied to the unknown folder, destination is logged, and the original is deleted.
4. The loop repeats until the queue has no `.tif` files, then exits until the next timer tick.

**Database:** The service uses the **Common** database (e.g. on `okc-docld`) with Windows Authentication. Stored procedures used include `GetLocationCollatorPaths`, `ListSirmDocumentTypeInfo`, `GetDoclinkPropertys`, and `InsertImageIoMessageDestination`.

---

## Prerequisites

- **.NET Framework 4.8**
- **SQL Server** access to the Common (and Doclink) databases
- **Doclink** server reachable (endpoint and credentials in config)
- **Network share** access to the configured `QueueFolder`, `ValidationDirectory`, and unknown/default folder
- **Windows Service** run account with permissions to the above (DB, Doclink, file shares)

---

## Configuration

Configuration is in **App.config** (for the Windows Service: `SirmEdmImageProcessingWS\App.config`).

Important settings (under `Maxum.EDM.Properties.Settings` or application settings):

- **QueueFolder** – Folder to scan for `.tif` files (e.g. `\\server\SIRM_Queue\Completed`).
- **ValidationDirectory** – Where validation XML and copied TIFFs are written for recognized SIRM documents.
- **DefalutUnknownFolder** – Default destination for unrecognized documents when no collator path exists.
- **TimerIntervalInMinutes** – How often the service runs the processing cycle (e.g. `0.25` = 15 seconds).
- **Doclink:** `Auth_DL_Server`, `Auth_DL_DB`, `Auth_DL_User`, `Auth_DL_PW`, **DoclinkEndpoint** (e.g. `tcp://server:5555/doclinkServer.soap`).

Connection strings point to the Common database and (where used) the Doclink database; typically `Integrated Security=True` for the service account.

---

## Logging

- **NLog** is used for file and (optionally) console and email (see `NLog.config` in the service project).
- Logs are written under the service directory (e.g. `logs\`) and optionally to the Windows Event Log and/or email for Errors.
- Step-based messages (e.g. `Step 3.8.4.x`) make it easy to trace each file and Doclink/workflow steps.

---

## Running

- **As a Windows Service:** Install via the setup project or `sc create` / `InstallUtil`, then start the service. It will run on the configured timer.
- **Console (testing):** Run **TestConsole** or **SirmEdmImageProcessing** console host; it will run one processing cycle and exit unless the host is written to loop.

---

## Security notes

- Do **not** log Doclink credentials. Ensure no `Logger` call includes the password (e.g. remove `Auth_DL_PW` from any log message parameters).
- Prefer storing Doclink password in a secure mechanism (e.g. encrypted config or secret store) rather than plain text in `App.config`, especially if config is in source control.
- The service account must have minimal required rights to the queue folder, validation folder, unknown folder, Common DB, and Doclink.

---

## Deployment

1. Build the solution in **Release** (e.g. Release | x86 for the service).
2. Use **SirmEdmProcessingSetup** to install the Windows Service, or copy the service binaries and install with `InstallUtil.exe` or `sc`.
3. Update **App.config** and **NLog.config** for the target environment (paths, Doclink endpoint, DB server, mail targets if used).
4. Create the Event Log source if required (e.g. “SIRM Document Routing” or as configured), or ensure the service has permission to create it.
5. Start the service and confirm logs show timer runs and file processing as expected.