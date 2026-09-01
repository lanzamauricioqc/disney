import { useEffect, useRef, type ReactNode } from "react";

export function Modal({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  const ref = useRef<HTMLDialogElement>(null); useEffect(() => { ref.current?.showModal(); }, []);
  return <dialog ref={ref} className="modal" onCancel={onClose} onClick={(event) => { if (event.target === ref.current) onClose(); }}><div className="modal-head"><h2>{title}</h2><button onClick={onClose} aria-label="Close dialog">×</button></div>{children}</dialog>;
}
