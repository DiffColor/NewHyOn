declare module '@horizon/client' {
  interface HorizonOptions {
    host?: string;
    secure?: boolean;
    lazyWrites?: boolean;
  }

  export default function Horizon(options?: HorizonOptions): unknown;
}
