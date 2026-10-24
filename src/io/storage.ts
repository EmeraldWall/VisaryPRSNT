import type { Presentation } from '../model/types';

const DB_NAME = 'visary-prsnt';
const STORE = 'docs';

interface Record {
  id: string;
  title: string;
  updatedAt: number;
  pres: Presentation;
}

export interface DocMeta {
  id: string;
  title: string;
  updatedAt: number;
  pres: Presentation;
}

const memory = new Map<string, Record>();
let dbPromise: Promise<IDBDatabase | null> | null = null;

function db(): Promise<IDBDatabase | null> {
  if (!dbPromise) {
    dbPromise = new Promise((resolve) => {
      try {
        const req = indexedDB.open(DB_NAME, 1);
        req.onupgradeneeded = () => {
          if (!req.result.objectStoreNames.contains(STORE)) req.result.createObjectStore(STORE, { keyPath: 'id' });
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => resolve(null);
        req.onblocked = () => resolve(null);
      } catch {
        resolve(null);
      }
    });
  }
  return dbPromise;
}

function tx<T>(mode: IDBTransactionMode, fn: (store: IDBObjectStore) => IDBRequest<T>): Promise<T | undefined> {
  return db().then((d) => {
    if (!d) return undefined;
    return new Promise<T | undefined>((resolve, reject) => {
      const t = d.transaction(STORE, mode);
      const req = fn(t.objectStore(STORE));
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  });
}

export async function saveDoc(pres: Presentation): Promise<void> {
  const rec: Record = { id: pres.id, title: pres.title, updatedAt: pres.updatedAt, pres };
  const d = await db();
  if (!d) {
    memory.set(pres.id, rec);
    return;
  }
  await tx('readwrite', (s) => s.put(rec));
}

export async function loadDoc(id: string): Promise<Presentation | null> {
  const d = await db();
  if (!d) return memory.get(id)?.pres ?? null;
  const rec = await tx<Record>('readonly', (s) => s.get(id));
  return rec?.pres ?? null;
}

export async function listDocs(): Promise<DocMeta[]> {
  const d = await db();
  const all = d ? ((await tx<Record[]>('readonly', (s) => s.getAll())) ?? []) : [...memory.values()];
  return all.sort((a, b) => b.updatedAt - a.updatedAt);
}

export async function deleteDoc(id: string): Promise<void> {
  const d = await db();
  if (!d) {
    memory.delete(id);
    return;
  }
  await tx('readwrite', (s) => s.delete(id));
}
