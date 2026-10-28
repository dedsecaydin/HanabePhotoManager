"""Copy and verify pending HanabePhoto imports without overwriting existing files.

The source and existing destination files are preserved. A verified group is
removed from the application's resume journal only after both files are safe.
"""

import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
from datetime import datetime

APP_DATA = Path(os.environ['LOCALAPPDATA']) / 'HanabePhotoManager'
JOURNAL = APP_DATA / 'import-resume.json'
LOG_DIR = Path(__file__).resolve().parents[1] / '.artifacts' / 'pending-import-recovery'
LOG_DIR.mkdir(parents=True, exist_ok=True)
LOG = LOG_DIR / 'progress.log'
PREFIX = '副机保留_20260928_'


def write_log(message):
    line = f'{datetime.now().isoformat(timespec="seconds")} {message}'
    with LOG.open('a', encoding='utf-8') as handle:
        handle.write(line + '\n')
    print(line, flush=True)


def digest(path):
    result = hashlib.sha256()
    with open(path, 'rb', buffering=0) as handle:
        while block := handle.read(8 * 1024 * 1024):
            result.update(block)
    return result.digest()


def copy_verified(source, destination):
    if destination.exists():
        if source.stat().st_size == destination.stat().st_size and digest(source) == digest(destination):
            return 'already verified'
        raise RuntimeError(f'alternate destination is occupied: {destination}')
    temporary = destination.with_name(destination.name + '.hanabe-recovery-part')
    if temporary.exists():
        temporary.unlink()
    source_hash = hashlib.sha256()
    with source.open('rb', buffering=0) as input_file, temporary.open('xb', buffering=0) as output_file:
        while block := input_file.read(8 * 1024 * 1024):
            source_hash.update(block)
            output_file.write(block)
        output_file.flush()
        os.fsync(output_file.fileno())
    if source.stat().st_size != temporary.stat().st_size or source_hash.digest() != digest(temporary):
        raise RuntimeError(f'copy verification failed: {source}')
    if destination.exists():
        raise RuntimeError(f'alternate destination appeared during copy: {destination}')
    temporary.rename(destination)
    shutil.copystat(source, destination)
    return 'copied and verified'


def save_journal(state):
    temporary = JOURNAL.with_suffix('.json.recovery-tmp')
    with temporary.open('w', encoding='utf-8') as handle:
        json.dump(state, handle, ensure_ascii=False, indent=2)
        handle.flush()
        os.fsync(handle.fileno())
    os.replace(temporary, JOURNAL)


def main():
    original = JOURNAL.read_bytes()
    state = json.loads(original)
    backup = LOG_DIR / 'import-resume.before-recovery.json'
    if not backup.exists():
        backup.write_bytes(original)
    entries = state['Entries']
    write_log(f'starting {len(entries)} groups; sources will remain in place')
    failures = 0
    for number, entry in enumerate(list(entries), 1):
        group = entry['GroupKey']
        files = entry['Plan']['Files']
        try:
            for item in files:
                source = Path(item['Source']['FullPath'])
                if not source.is_file():
                    raise FileNotFoundError(source)
                existing = Path(item['DestinationPath'])
                destination = (existing.with_name(PREFIX + existing.name)
                               if item['Conflict'] == 2 else existing)
                if not destination.parent.is_dir():
                    raise FileNotFoundError(destination.parent)
                result = copy_verified(source, destination)
                write_log(f'{number}/{len(entries)} {group}: {destination.name} {result}')
            state['Entries'] = [other for other in state['Entries'] if other is not entry]
            save_journal(state)
        except Exception as error:
            failures += 1
            write_log(f'{number}/{len(entries)} {group}: FAILED {error}')
    pending = len(json.loads(JOURNAL.read_bytes())['Entries'])
    write_log(f'finished; verified groups {len(entries)}, journal pending {pending}; transient errors {failures}')
    return 0 if pending == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
