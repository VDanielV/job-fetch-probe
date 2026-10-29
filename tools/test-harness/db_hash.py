"""Prints one fingerprint of every row of every table in a SQLite database, to show that something changed nothing
(take it before and after, compare). Python 3, standard library only.

  python -I tools/test-harness/db_hash.py <folder>/database/jobs.db
"""
import hashlib
import sqlite3
import sys


def main():
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    db = sqlite3.connect(f"file:{sys.argv[1]}?mode=ro", uri=True)
    digest = hashlib.md5()
    for (table,) in db.execute("select name from sqlite_master where type = 'table' order by name"):
        for row in db.execute(f'select * from "{table}" order by 1'):
            digest.update(repr(row).encode())
    print(digest.hexdigest())


if __name__ == "__main__":
    main()
