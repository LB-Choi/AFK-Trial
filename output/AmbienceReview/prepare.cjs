const fs=require('fs');
const source='F:/Battlefield Sounds Pro/Base at day/Base at day _03.wav';
const b=fs.readFileSync(source);let start,size,rate,channels,bits,format;
for(let p=12;p+8<=b.length;){const n=b.readUInt32LE(p+4),id=b.toString('ascii',p,p+4);if(id==='fmt '){format=b.readUInt16LE(p+8);channels=b.readUInt16LE(p+10);rate=b.readUInt32LE(p+12);bits=b.readUInt16LE(p+22);}if(id==='data'){start=p+8;size=n;}p+=8+n+(n%2);}
if(format!==1||bits!==16||channels!==2)throw Error('Expected stereo PCM16');
const frames=size/(channels*2),fade=rate,outputFrames=frames-fade;
const out=Buffer.alloc(44+outputFrames*channels*2);
out.write('RIFF');out.writeUInt32LE(out.length-8,4);out.write('WAVEfmt ',8);out.writeUInt32LE(16,16);out.writeUInt16LE(1,20);out.writeUInt16LE(channels,22);out.writeUInt32LE(rate,24);out.writeUInt32LE(rate*channels*2,28);out.writeUInt16LE(channels*2,32);out.writeUInt16LE(16,34);out.write('data',36);out.writeUInt32LE(out.length-44,40);
let peak=0,sum=0;
for(let i=0;i<outputFrames;i++)for(let c=0;c<channels;c++){
 let value;
 if(i<frames-2*fade)value=b.readInt16LE(start+((i+fade)*channels+c)*2);
 else {const j=i-(frames-2*fade),t=(j+1)/fade;value=(1-t)*b.readInt16LE(start+((frames-fade+j)*channels+c)*2)+t*b.readInt16LE(start+(j*channels+c)*2);}
 value=Math.round(value);peak=Math.max(peak,Math.abs(value)/32768);sum+=(value/32768)**2;out.writeInt16LE(value,44+(i*channels+c)*2);
}
const seam=[0,1].map(c=>Math.abs(out.readInt16LE(44+c*2)-out.readInt16LE(out.length-(channels-c)*2))/32768);
if(peak>=1||seam.some(v=>v>.05))throw Error('Clipping or excessive loop boundary');
fs.writeFileSync('LOF/Assets/Resources/Audio/Background.wav',out);
fs.writeFileSync('output/AmbienceReview/preparation.json',JSON.stringify({source,seconds:outputFrames/rate,crossfadeSeconds:1,peak,rms:Math.sqrt(sum/(outputFrames*channels)),seam},null,2));
console.log(fs.readFileSync('output/AmbienceReview/preparation.json','utf8'));
