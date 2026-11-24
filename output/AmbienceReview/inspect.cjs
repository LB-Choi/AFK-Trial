const fs=require('fs'), path=require('path');
const root='F:/Battlefield Sounds Pro';
const results=[];
for(const dir of fs.readdirSync(root,{withFileTypes:true}).filter(d=>d.isDirectory())) for(const name of fs.readdirSync(path.join(root,dir.name)).filter(n=>n.endsWith('.wav'))){
 const file=path.join(root,dir.name,name), b=fs.readFileSync(file);let fmt,start,size;
 for(let p=12;p+8<=b.length;){let n=b.readUInt32LE(p+4);if(b.toString('ascii',p,p+4)==='fmt ')fmt={format:b.readUInt16LE(p+8),channels:b.readUInt16LE(p+10),rate:b.readUInt32LE(p+12),bits:b.readUInt16LE(p+22)};if(b.toString('ascii',p,p+4)==='data'){start=p+8;size=n;}p+=8+n+(n%2);}
 let sum=0,peak=0,count=0;const step=fmt.bits/8;
 if(fmt.format===1 && [16,24,32].includes(fmt.bits)) for(let p=start;p<start+size;p+=step*16){let v=fmt.bits===16?b.readInt16LE(p)/32768:fmt.bits===24?b.readIntLE(p,3)/8388608:b.readInt32LE(p)/2147483648;sum+=v*v;peak=Math.max(peak,Math.abs(v));count++;}
 results.push({file,...fmt,seconds:size/(fmt.rate*fmt.channels*step),rms:Math.sqrt(sum/count),peak});
}
fs.writeFileSync('output/AmbienceReview/inventory.json',JSON.stringify(results,null,2));
console.log(JSON.stringify(results.filter(r=>/Base at day|Inside base building\\Inside|Distant battle at day/.test(r.file)),null,2));
