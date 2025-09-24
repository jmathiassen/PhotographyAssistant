# Photograpy Assistant
This is a .NET 8 background service designed for Debian to automatically and safely import photos from sources like SD cards, optionally create multiple backups to local hard drives, and optionally transfer them to remote servers via SCP.

---
## Key Features

* **Headless Operation:** The project is designed to run as a **daemon** on a headless Linux machine, evidenced by the included `photography-assistant.service` file for `systemd`.
* **Automated Import:** It automatically detects and imports files from configured source directories based on a predefined list of file extensions. To prevent accidental imports from connected backup drives, you can place a simple .exclude_this_drive file (the name is configurable) in the root of any drive you wish to be ignored.
* **Data Safety:** The workflow prioritizes data integrity by using a **"copy, verify, then delete"** strategy. An original file is never deleted until it has been safely duplicated in the next stage of the pipeline.
* **Intelligent Renaming:** It can optionally read a photo's EXIF metadata to prepend the original date and time to the filename, ensuring uniqueness and chronological sorting.
* **Stateful Processing:** The service intelligently tracks which SD cards have been fully processed, to only process an SD card once. To re-process a card, simply eject and re-insert it, and it will be processed again on the next cycle.
* **Flexible Backups and Drive Pooling:** The system can simultaneously back up files to multiple destinations, with each destination being individually activated in the configuration. The available destinations are remote servers via SCP, and local drive groups consisting of one or more physical disks.
* **Enhanced Reliability and Load Balancing:** The application adds two layers of intelligence to its backup process. First, it proactively checks if a destination disk has enough free space before attempting a copy, preventing errors from full drives. Second, when a drive group has multiple physical disks connected, the service will automatically load-balance by copying the next file to the disk with the most available free space, ensuring your storage pool fills up efficiently and evenly.
* **Safe Configuration:** A `config.json` file is automatically generated on the first run with safe defaults, requiring you to explicitly opt-in to enable backup and transfer features.

---
## Technology Stack

The application is built on a modern .NET foundation, using a set of well-chosen libraries for its tasks:

* **Framework:** **.NET 8**
* **Service Hosting:** **Microsoft.Extensions.Hosting**, the standard framework for creating robust, long-running background services in .NET.
* **Metadata:** **MetadataExtractor** for reliably reading EXIF and other metadata from image files.
* **Remote Transfers:** **SSH.NET** for handling secure file transfers using the SCP protocol.

---
## Directory Structure
```
/photography-assistant/
    photography-assistant         - The compiled application executable
    config.json                   - Main configuration file
    photography-assistant.service - systemd unit file for running as a daemon
    /import/
        .exclude_this_drive       - (Optional) An empty file to mark a drive to be ignored
    /data/
        /spool/
            /import/
                /incoming/        - STAGE 1 (Ingestion): Files are copied here from the source.
                /promote/         - STAGE 2 (Promotion): Files are moved here, ready for distribution.
            /external/
                /drive1/
                    /incoming/    - STAGE 3a (Distribution): A copy of a file is placed here.
                    /promote/     - STAGE 4a (Staging): The file is moved here, ready for final transfer.
                /drive2/
                    /incoming/    - STAGE 3a (Distribution): A copy of a file is placed here.
                    /promote/     - STAGE 4a (Staging): The file is moved here, ready for final transfer.
            /remote/
                /host1/
                    /incoming/    - STAGE 3b (Distribution): A copy of a file is placed here.
                    /promote/     - STAGE 4b (Staging): The file is moved here, ready for upload.
        /storage/
            # Physical paths for the 'drive1' group
            /hdd1/                - STAGE 5: Final storage location (part of group 1, name is configurable)
            /ssd1/                - STAGE 5: Final storage location (part of group 1, name is configurable)

            # Physical paths for the 'drive2' group
            /hdd2/                - STAGE 5: Final storage location (part of group 2, name is configurable)
            /ssd2/                - STAGE 5: Final storage location (part of group 2, name is configurable)

---
## Workflow

The application uses a careful, multi-stage pipeline to ensure data is never lost during processing. Clarifying the steps outlined in the README, the data flow for a single file is as follows:

1.  **Ingestion:** A file is copied from a source (e.g., an SD card) into a central `spool/import/incoming` directory. The original is deleted only after the copy is verified.
2.  **Promotion:** The verified file is moved from `incoming` to `spool/import/promote` (referred to as `imported` in the README), marking it ready for distribution.
3.  **Distribution (Demux):** The file in `promote` is copied to the `incoming` directory of every active external hard drive and remote host destination.
4.  **Cleanup:** Once the file has been successfully copied to all active destinations, it is deleted from the `spool/import/promote` directory.
5.  **Staging:** Each destination then moves its verified copy from its `incoming` directory to its own `promote` directory.
6.  **Final Transfer:**
    * For external hard drives, the service analyzes the destination drive group. It identifies all connected physical disks that have sufficient free space for the file. From that list of suitable disks, it selects the one with the most available free space and copies the file there. The staged copy is deleted after verification.
    * For **remote hosts**, the file is uploaded from the `promote` directory via SCP. The local staged copy is deleted after a successful upload.